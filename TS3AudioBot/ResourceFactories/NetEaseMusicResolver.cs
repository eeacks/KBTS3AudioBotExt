// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class NetEaseMusicResolver : IResourceResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex LinkMatch = new Regex(
			@"^(https?\:\/\/)?((music|y)\.)?163\.com",
			Util.DefaultRegexConfig);
		private readonly string tempDownloadDir = Path.Combine(Path.GetTempPath(), "ts3audiobot_ytdl");

		public string ResolverFor => "netease";

		public MatchCertainty MatchResource(ResolveContext _, string uri)
			=> LinkMatch.IsMatch(uri) ? MatchCertainty.Always : MatchCertainty.Never;

		public Task<PlayResource> GetResource(ResolveContext ctx, string uri)
			=> GetResourceById(ctx, new AudioResource(uri, null, ResolverFor));

		public async Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource)
		{
			Log.Debug("Using yt-dlp for netease music resource: {0}", resource.ResourceId);

			var response = await YoutubeDlHelper.GetSingleVideo(resource.ResourceId);
			resource.ResourceTitle = response.AutoTitle ?? $"NetEase-{resource.ResourceId}";

			var songInfo = YoutubeDlHelper.MapToSongInfo(response);
			var format = YoutubeDlHelper.FilterBestEnhanced(response.formats);
			var url = format?.url;

			if (string.IsNullOrWhiteSpace(url))
			{
				Log.Warn("No suitable netease stream URL found for {0}. Falling back to direct download.", resource.ResourceId);
				return await DownloadFallback(resource, songInfo);
			}

			Log.Info("Selected netease format for {0}: format_id={1}, codec={2}",
				resource.ResourceId,
				format?.format_id ?? "unknown",
				format?.acodec ?? "unknown");

			if (YoutubeDlHelper.IsHlsManifest(url))
			{
				Log.Warn("Selected netease format for {0} is an HLS manifest. Falling back to direct download for reliability.", resource.ResourceId);
				return await DownloadFallback(resource, songInfo);
			}

			return new PlayResource(url, resource, songInfo: songInfo)
			{
				RequestHeaders = format?.http_headers
			};
		}

		private async Task<PlayResource> DownloadFallback(AudioResource resource, SongInfo songInfo)
		{
			var downloadResult = await YoutubeDlHelper.DownloadVideo(resource.ResourceId, tempDownloadDir);
			if (!downloadResult.Ok)
			{
				Log.Error("NetEase direct download fallback failed for {0}: {1}", resource.ResourceId, downloadResult.Error);
				throw Error.LocalStr(downloadResult.Error);
			}

			Log.Info("NetEase direct download fallback succeeded for {0}: {1}", resource.ResourceId, downloadResult.Value);
			return new PlayResource(downloadResult.Value, resource, songInfo: songInfo)
			{
				IsTemporaryFile = true,
				TemporaryFilePath = downloadResult.Value
			};
		}

		public string RestoreLink(ResolveContext _, AudioResource resource) => resource.ResourceId;

		public void Dispose() { }
	}
}
