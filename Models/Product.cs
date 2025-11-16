using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SAProject.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Tên sản phẩm là bắt buộc")]
        [Display(Name = "Tên sản phẩm")]
        public string Name { get; set; }

        [DataType(DataType.Currency)]
        [Display(Name = "Giá")]
        [Range(0, double.MaxValue, ErrorMessage = "Giá phải lớn hơn 0")]
        public decimal Price { get; set; }

        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        // Navigation property for images
        public virtual ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();

        [NotMapped]
        [Display(Name = "Tải lên hình ảnh")]
        public List<IFormFile>? ImageFiles { get; set; }
    }

    public class ProductImage
    {
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; }

        [Required]
        public string FileName { get; set; }

        public bool IsPrimary { get; set; }

        public int ProductId { get; set; }
        public virtual Product Product { get; set; }
    }
}