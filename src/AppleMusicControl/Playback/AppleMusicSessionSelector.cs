namespace AppleMusicControl.Playback;

internal static class AppleMusicSessionSelector
{
	// Apple Music's packaged app id contains "AppleMusic" (for example AppleInc.AppleMusicWin_...!App).
	private const string AppIdMarker = "AppleMusic";

	public static bool IsAppleMusic(string? sourceAppId)
		=> sourceAppId is not null && sourceAppId.Contains(AppIdMarker, StringComparison.OrdinalIgnoreCase);

	/// <summary>Picks the Apple Music session, preferring one that is playing when there are several.</summary>
	public static T? Select<T>(IEnumerable<T> sessions, Func<T, string?> appId, Func<T, bool> isPlaying)
		where T : class
	{
		T? first = null;
		foreach (var session in sessions)
		{
			if (!IsAppleMusic(appId(session)))
			{
				continue;
			}

			if (isPlaying(session))
			{
				return session;
			}

			first ??= session;
		}

		return first;
	}
}
