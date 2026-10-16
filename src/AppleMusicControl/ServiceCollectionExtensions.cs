using AppleMusicControl.Playback;
using AppleMusicControl.Volume;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
#if WINDOWS
using AppleMusicControl.Platform;
#endif

namespace AppleMusicControl;

internal static class ServiceCollectionExtensions
{
	public static IServiceCollection AddAppleMusic(this IServiceCollection services)
	{
#if WINDOWS
		services.TryAddSingleton<IMediaSessionSource, GsmtcMediaSessionSource>();
		services.TryAddSingleton<ISystemVolume, CoreAudioSystemVolume>();
#else
		services.TryAddSingleton<IMediaSessionSource, UnsupportedMediaSessionSource>();
		services.TryAddSingleton<ISystemVolume, UnsupportedSystemVolume>();
#endif
		services.TryAddSingleton(TimeProvider.System);
		services.TryAddSingleton<AppleMusicPlayer>();
		return services;
	}
}
