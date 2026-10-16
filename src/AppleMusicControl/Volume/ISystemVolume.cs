namespace AppleMusicControl.Volume;

/// <summary>The Windows master volume of the default playback device, in percent.</summary>
internal interface ISystemVolume
{
	/// <summary>Returns null when the volume cannot be read (no playback device, not Windows).</summary>
	int? TryGetPercent();

	/// <exception cref="InvalidOperationException">The volume could not be set.</exception>
	void SetPercent(int percent);
}

internal sealed class UnsupportedSystemVolume : ISystemVolume
{
	public int? TryGetPercent() => null;

	public void SetPercent(int percent) => throw new InvalidOperationException("System volume is only supported on Windows.");
}
