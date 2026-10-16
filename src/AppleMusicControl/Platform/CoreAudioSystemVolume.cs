#if WINDOWS
using System.Runtime.InteropServices;
using AppleMusicControl.Volume;

namespace AppleMusicControl.Platform;

/// <summary>Master volume of the default render endpoint through the Windows Core Audio API.</summary>
internal sealed class CoreAudioSystemVolume : ISystemVolume
{
	public int? TryGetPercent()
	{
		try
		{
			return WithEndpointVolume(volume =>
			{
				Marshal.ThrowExceptionForHR(volume.GetMasterVolumeLevelScalar(out var level));
				return (int)Math.Round(level * 100);
			});
		}
		catch (Exception ex) when (ex is COMException or InvalidCastException)
		{
			return null;
		}
	}

	public void SetPercent(int percent)
	{
		try
		{
			WithEndpointVolume(volume =>
			{
				var context = Guid.Empty;
				Marshal.ThrowExceptionForHR(volume.SetMasterVolumeLevelScalar(Math.Clamp(percent, 0, 100) / 100f, ref context));
				return 0;
			});
		}
		catch (Exception ex) when (ex is COMException or InvalidCastException)
		{
			throw new InvalidOperationException("Setting the system volume failed.", ex);
		}
	}

	// A fresh endpoint per call follows default device changes (headphones plugged in) without notifications.
	private static T WithEndpointVolume<T>(Func<CoreAudioInterop.IAudioEndpointVolume, T> use)
	{
		var enumerator = (CoreAudioInterop.IMMDeviceEnumerator)new CoreAudioInterop.MMDeviceEnumerator();
		CoreAudioInterop.IMMDevice? device = null;
		object? activated = null;
		try
		{
			Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(
				CoreAudioInterop.EDataFlow.Render, CoreAudioInterop.ERole.Multimedia, out device));
			var iid = typeof(CoreAudioInterop.IAudioEndpointVolume).GUID;
			Marshal.ThrowExceptionForHR(device.Activate(ref iid, CoreAudioInterop.ClsCtxAll, IntPtr.Zero, out activated));
			return use((CoreAudioInterop.IAudioEndpointVolume)activated);
		}
		finally
		{
			Release(activated);
			Release(device);
			Release(enumerator);
		}
	}

	private static void Release(object? comObject)
	{
		if (comObject is not null && Marshal.IsComObject(comObject))
		{
			Marshal.ReleaseComObject(comObject);
		}
	}
}
#endif
