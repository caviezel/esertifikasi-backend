namespace Esertifikasi.Api.Domain.Entities;

public interface ISoftDeletable {
  bool IsDeleted { get; set; }
  DateTimeOffset? DeletedAt { get; set; }
}
