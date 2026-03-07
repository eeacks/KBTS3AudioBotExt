// TS3AudioBot - An advanced Musicbot for Teamspeak 3
// Copyright (C) 2017  TS3AudioBot contributors
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the Open Software License v. 3.0
//
// You should have received a copy of the Open Software License along with this
// program. If not, see <https://opensource.org/licenses/OSL-3.0>.

using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TS3AudioBot.Helper;
using TS3AudioBot.Localization;

namespace TS3AudioBot.ResourceFactories
{
	public sealed class BilibiliResolver : IResourceResolver
	{
		private static readonly NLog.Logger Log = NLog.LogManager.GetCurrentClassLogger();
		private static readonly Regex LinkMatch = new Regex(
			@"^(https?\:\/\/)?((www\.|m\.)?bilibili\.com|b23\.tv)",
			Util.DefaultRegexConfig);

		public string ResolverFor => "bilibili";

		public MatchCertainty MatchResource(ResolveContext _, string uri)
			=> LinkMatch.IsMatch(uri) ? MatchCertainty.Always : MatchCertainty.Never;

		public Task<PlayResource> GetResource(ResolveContext ctx, string uri)
			=> GetResourceById(ctx, new AudioResource(uri, null, ResolverFor));

		public async Task<PlayResource> GetResourceById(ResolveContext _, AudioResource resource)
		{
			Log.Debug("Using yt-dlp for bilibili resource: {0}", resource.ResourceId);

			var response = await YoutubeDlHelper.GetSingleVideo(resource.ResourceId);
			resource.ResourceTitle = response.AutoTitle ?? $"Bilibili-{resource.ResourceId}";

			var songInfo = YoutubeDlHelper.MapToSongInfo(response);
			var format = YoutubeDlHelper.FilterBestEnhanced(response.formats);
			var url = format?.url;

			if (string.IsNullOrWhiteSpace(url))
				throw Error.LocalStr(strings.error_ytdl_empty_response);

			Log.Info("Selected bilibili format for {0}: format_id={1}, codec={2}",
				resource.ResourceId,
				format?.format_id ?? "unknown",
				format?.acodec ?? "unknown");

			return new PlayResource(url, resource, songInfo: songInfo);
		}

		public string RestoreLink(ResolveContext _, AudioResource resource) => resource.ResourceId;

		public void Dispose() { }
	}
}
