namespace EduMindX.SmartBoard.Core;

/// <summary>
/// PIN kontrolü sonucunda dönen sonuç ve eşleşen kod hash'i (eğer tek kullanımlık kod ise).
/// </summary>
/// <param name="Result">Parola kontrolünün sonucu (Başarılı, Yanlış, Kilitli vb.).</param>
/// <param name="CodeHash">Eğer girilen PIN tek kullanımlık bir günlük kod ile eşleştiyse onun hash'i; ana şifre ise null.</param>
public sealed record PinCheck(PasswordResult Result, string? CodeHash);
