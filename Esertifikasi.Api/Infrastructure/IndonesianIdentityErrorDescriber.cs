using Microsoft.AspNetCore.Identity;

namespace Esertifikasi.Api.Infrastructure;

public sealed class IndonesianIdentityErrorDescriber : IdentityErrorDescriber {
  public override IdentityError DefaultError() => Error(nameof(DefaultError), "Terjadi kesalahan saat memproses akun.");
  public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Data akun telah berubah. Silakan coba kembali.");
  public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "Nama pengguna tidak valid.");
  public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Alamat email tidak valid.");
  public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Nama pengguna sudah digunakan.");
  public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Alamat email sudah digunakan.");
  public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"Kata sandi minimal terdiri dari {length} karakter.");
  public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "Kata sandi harus memiliki minimal satu karakter khusus.");
  public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "Kata sandi harus memiliki minimal satu angka.");
  public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "Kata sandi harus memiliki minimal satu huruf kecil.");
  public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "Kata sandi harus memiliki minimal satu huruf besar.");
  public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"Kata sandi harus memiliki minimal {uniqueChars} karakter yang berbeda.");
  public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Login eksternal sudah terhubung dengan akun lain.");
  public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "Kode pemulihan tidak valid.");

  private static IdentityError Error(string code, string description) {
    return new IdentityError { Code = code, Description = description };
  }
}
