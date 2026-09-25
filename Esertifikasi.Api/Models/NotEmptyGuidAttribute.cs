using System.ComponentModel.DataAnnotations;

namespace Esertifikasi.Api.Models;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter)]
public sealed class NotEmptyGuidAttribute : ValidationAttribute {
  public NotEmptyGuidAttribute() : base("The {0} field must contain a non-empty identifier.") { }

  public override bool IsValid(object? value) {
    return value is Guid id && id != Guid.Empty;
  }
}
