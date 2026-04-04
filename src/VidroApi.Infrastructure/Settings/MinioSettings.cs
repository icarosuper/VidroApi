using System.ComponentModel.DataAnnotations;

namespace VidroApi.Infrastructure.Settings;

public class MinioSettings
{
    [Required]
    public string Endpoint { get; set; } = null!;

    [Required]
    public string AccessKey { get; set; } = null!;

    [Required]
    public string SecretKey { get; set; } = null!;

    [Required]
    public string BucketName { get; set; } = null!;

    public bool UseSsl { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int UploadUrlTtlHours { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int ThumbnailUrlTtlHours { get; set; }

    [Required, Range(1, int.MaxValue)]
    public int VideoUrlTtlHours { get; set; }
}
