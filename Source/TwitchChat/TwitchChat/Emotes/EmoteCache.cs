using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using UnityEngine;

namespace TwitchChat.Emotes
{
	// two-tier cache for low-memory machines: bytes on disk, a small LRU of decoded textures; Get() main-thread, fetch/decode off-thread
	public static class EmoteCache
	{
		// caps bound OOM/hang; no auto-redirect so an allowed host can't 302 the fetch to an internal one
		private static readonly HttpClient Http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
		{
			Timeout = TimeSpan.FromSeconds(15),
			MaxResponseContentBufferSize = 8L * 1024 * 1024,
		};

		// Decoded textures, oldest first in lru. Touched only on the main thread, so no lock needed.
		private const int MaxTextures = 128;
		// Reject a decoded image bigger than this on either axis (a pixel bomb); real emotes top out near 128px.
		private const int MaxEmoteDim = 256;
		private static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
		private static readonly List<string> lru = new List<string>();

		// Bytes fetched off-thread, waiting to be turned into a texture on the main thread.
		private static readonly ConcurrentDictionary<string, byte[]> ready = new ConcurrentDictionary<string, byte[]>();

		// URLs in flight, and URLs permanently rejected (undecodable format, or bytes that won't decode).
		private static readonly object gate = new object();
		private static readonly HashSet<string> inFlight = new HashSet<string>();
		private static readonly HashSet<string> rejected = new HashSet<string>();

		// a network/IO failure is transient, so back off (UtcNow works off-thread, Time.time doesn't) and let Get retry
		private static readonly ConcurrentDictionary<string, DateTime> failedAt = new ConcurrentDictionary<string, DateTime>();
		private const double RetryBackoffSec = 15.0;

		private static string dir;
		private const long MaxDiskBytes = 50L * 1024 * 1024;

		// Re-prune the disk cache every so many writes; the startup prune alone doesn't hold the cap mid-session.
		private static int writesSincePrune;
		private static int pruning;
		private const int PruneEveryWrites = 64;

		// The texture for a URL if it's ready, otherwise null while it loads (kicks off the load once).
		public static Texture2D Get(string url)
		{
			if (string.IsNullOrEmpty(url)) return null;
			lock (gate)
			{
				if (rejected.Contains(url)) return null;
			}

			EnsureDir();

			if (textures.TryGetValue(url, out Texture2D tex))
			{
				Touch(url);
				return tex;
			}

			// A worker staged the bytes; build the texture now (we're on the main thread).
			if (ready.TryRemove(url, out byte[] bytes))
			{
				tex = Decode(bytes);
				if (tex == null)
				{
					Reject(url);
					return null;
				}
				failedAt.TryRemove(url, out _);
				Add(url, tex);
				return tex;
			}

			if (Undecodable(url) || !SafeToFetch(url))
			{
				Reject(url);
				return null;
			}

			// A recent fetch failed; wait out the backoff before hammering the CDN again.
			if (failedAt.TryGetValue(url, out DateTime when) && (DateTime.UtcNow - when).TotalSeconds < RetryBackoffSec)
			{
				return null;
			}

			Fetch(url);
			return null;
		}

		private static Texture2D Decode(byte[] bytes)
		{
			if (HeaderTooBig(bytes)) return null; // reject a pixel bomb before LoadImage allocates the full texture
			var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
			if (tex.LoadImage(bytes) && tex.width <= MaxEmoteDim && tex.height <= MaxEmoteDim)
			{
				tex.filterMode = FilterMode.Bilinear;
				tex.wrapMode = TextureWrapMode.Clamp;
				return tex;
			}
			UnityEngine.Object.Destroy(tex);
			return null;
		}

		private static void Add(string url, Texture2D tex)
		{
			textures[url] = tex;
			lru.Add(url);
			if (textures.Count <= MaxTextures) return;

			string oldest = lru[0];
			lru.RemoveAt(0);
			if (textures.TryGetValue(oldest, out Texture2D old))
			{
				textures.Remove(oldest);
				UnityEngine.Object.Destroy(old); // free the VRAM; the bytes stay on disk
			}
		}

		private static void Touch(string url)
		{
			lru.Remove(url);
			lru.Add(url);
		}

		// Read from disk, or download and write to disk, then stage the bytes for the main thread.
		private static void Fetch(string url)
		{
			lock (gate)
			{
				if (!inFlight.Add(url)) return;
			}
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					string file = Path.Combine(dir, FileName(url));
					byte[] bytes = File.Exists(file) ? File.ReadAllBytes(file) : Download(url, file);
					if (bytes != null && bytes.Length > 0)
					{
						ready[url] = bytes;
					}
					else
					{
						failedAt[url] = DateTime.UtcNow;
					}
				}
				catch
				{
					// network hiccup, 5xx, timeout, or a torn read: all transient, so back off and retry rather than rejecting for the session
					failedAt[url] = DateTime.UtcNow;
				}
				finally
				{
					lock (gate)
					{
						inFlight.Remove(url);
					}
				}
			});
		}

		private static byte[] Download(string url, string file)
		{
			byte[] bytes = Http.GetByteArrayAsync(url).GetAwaiter().GetResult();
			try
			{
				File.WriteAllBytes(file, bytes);
				MaybePrune();
			}
			catch { /* disk full or unwritable: still usable this session from memory */ }
			return bytes;
		}

		private static void Reject(string url)
		{
			lock (gate)
			{
				rejected.Add(url);
			}
		}

		private static void EnsureDir()
		{
			if (dir != null) return;
			dir = Path.Combine(Application.persistentDataPath, "TwitchChatEmotes");
			Directory.CreateDirectory(dir);
			PruneDisk(dir);
		}

		private static void MaybePrune()
		{
			if (Interlocked.Increment(ref writesSincePrune) < PruneEveryWrites) return;
			Interlocked.Exchange(ref writesSincePrune, 0);
			PruneDisk(dir);
		}

		// keep the on-disk cache under the size cap, dropping least-recently-written first; off-thread so it doesn't stall the draw
		private static void PruneDisk(string d)
		{
			if (Interlocked.Exchange(ref pruning, 1) == 1) return; // one prune at a time
			ThreadPool.QueueUserWorkItem(_ =>
			{
				try
				{
					FileInfo[] files = new DirectoryInfo(d).GetFiles();
					long total = 0;
					foreach (FileInfo f in files)
					{
						total += f.Length;
					}
					if (total <= MaxDiskBytes) return;

					// Not LastAccessTime: Windows disables last-access updates by default, so it's unreliable.
					Array.Sort(files, (a, b) => a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc));
					foreach (FileInfo f in files)
					{
						if (total <= MaxDiskBytes) break;
						total -= f.Length;
						try { f.Delete(); } catch { }
					}
				}
				catch { }
				finally
				{
					Interlocked.Exchange(ref pruning, 0);
				}
			});
		}

		// Stable file name from the URL: a hash keeps it filesystem-safe, the extension keeps the format.
		private static string FileName(string url)
		{
			ulong h = 14695981039346656037UL;
			foreach (char c in url)
			{
				h ^= c;
				h *= 1099511628211UL;
			}
			return h.ToString("x16") + Extension(url);
		}

		private static string Extension(string url)
		{
			int query = url.IndexOf('?');
			string path = query >= 0 ? url.Substring(0, query) : url;
			int dot = path.LastIndexOf('.');
			string ext = dot >= 0 ? path.Substring(dot).ToLowerInvariant() : "";
			return ext.Length <= 5 ? ext : "";
		}

		// formats Unity can't decode (animated emotes); skip the download and show the name for now
		private static bool Undecodable(string url)
		{
			string ext = Extension(url);
			return ext == ".webp" || ext == ".avif" || ext == ".gif";
		}

		// emote URLs come from the 7TV/FFZ APIs and point at their CDNs; require https so a tampered response can't downgrade the fetch to http or another scheme
		private static bool SafeToFetch(string url)
		{
			return Uri.TryCreate(url, UriKind.Absolute, out Uri uri) && uri.Scheme == Uri.UriSchemeHttps;
		}

		// reject a PNG whose IHDR declares dimensions past MaxEmoteDim before LoadImage allocates it; PNG is what providers serve, a rare JPEG still hits the post-decode check
		private static bool HeaderTooBig(byte[] b)
		{
			if (b.Length < 24 || b[0] != 0x89 || b[1] != 0x50 || b[2] != 0x4E || b[3] != 0x47) return false;
			// IHDR width is bytes 16..19, height 20..23, big-endian; anything past MaxEmoteDim sets a high byte or shows in the low two
			if (b[16] != 0 || b[17] != 0 || b[20] != 0 || b[21] != 0) return true;
			int width = b[18] * 256 + b[19];
			int height = b[22] * 256 + b[23];
			return width > MaxEmoteDim || height > MaxEmoteDim;
		}
	}
}
