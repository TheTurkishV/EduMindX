using System.Reflection;

namespace EduMindX.SmartBoard;

/// <summary>
/// Uygulama derleme bilgilerini ve geçerli sürüm numarasını sağlayan yardımcı sınıf.
/// </summary>
internal static class AppInfo
{
	/// <summary>
	/// Derlemeden alınan semantik sürüm numarası (Örn: "1.5.1").
	/// </summary>
	public static readonly string Version = typeof(AppInfo).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
}
