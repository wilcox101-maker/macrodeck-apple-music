namespace AppleMusicControl.Playback;

internal static class ArtworkFormat
{
	/// <summary>Detects the image type from its signature; the OS content type is not always set.</summary>
	public static string? Detect(ReadOnlySpan<byte> data)
	{
		if (data.StartsWith((ReadOnlySpan<byte>)[0x89, 0x50, 0x4E, 0x47]))
		{
			return "image/png";
		}

		if (data.StartsWith((ReadOnlySpan<byte>)[0xFF, 0xD8, 0xFF]))
		{
			return "image/jpeg";
		}

		if (data.Length >= 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8))
		{
			return "image/webp";
		}

		if (data.StartsWith("GIF8"u8))
		{
			return "image/gif";
		}

		return null;
	}
}
