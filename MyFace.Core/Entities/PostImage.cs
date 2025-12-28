namespace MyFace.Core.Entities;

public class PostImage
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public string OriginalPath { get; set; } = string.Empty;
    public string ThumbnailPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public long FileSize { get; set; }
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }

    public Post Post { get; set; } = null!;
}
