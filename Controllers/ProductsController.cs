namespace SAProject.Controllers;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAProject.Data;
using SAProject.Models;

[Authorize(Roles = "Admin")]
public class AdminProductsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public AdminProductsController(ApplicationDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    // GET: AdminProducts
    public async Task<IActionResult> Index()
    {
        var products = await _context.Products
            .Include(p => p.Images)
            .ToListAsync();
        return View(products);
    }

    // GET: AdminProducts/Details/5
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        return View(product);
    }

    // GET: AdminProducts/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: AdminProducts/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Product product)
    {
        if (ModelState.IsValid)
        {
            _context.Add(product);
            await _context.SaveChangesAsync();

            // Handle image upload
            if (product.ImageFiles != null && product.ImageFiles.Any())
            {
                await SaveProductImages(product.Id, product.ImageFiles);
            }

            TempData["SuccessMessage"] = "Tạo sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    // GET: AdminProducts/Edit/5
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product == null)
        {
            return NotFound();
        }
        return View(product);
    }

    // POST: AdminProducts/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Product product)
    {
        if (id != product.Id)
        {
            return NotFound();
        }

        if (ModelState.IsValid)
        {
            try
            {
                _context.Update(product);
                await _context.SaveChangesAsync();

                // Handle image upload
                if (product.ImageFiles != null && product.ImageFiles.Any())
                {
                    await SaveProductImages(product.Id, product.ImageFiles);
                }

                TempData["SuccessMessage"] = "Cập nhật sản phẩm thành công!";
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!ProductExists(product.Id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }
            return RedirectToAction(nameof(Index));
        }
        return View(product);
    }

    // GET: AdminProducts/Delete/5
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(m => m.Id == id);

        if (product == null)
        {
            return NotFound();
        }

        return View(product);
    }

    // POST: AdminProducts/Delete/5
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var product = await _context.Products
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id);

        if (product != null)
        {
            // Delete associated images
            foreach (var image in product.Images)
            {
                DeleteImageFile(image.ImageUrl);
            }

            _context.Products.Remove(product);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa sản phẩm thành công!";
        }

        return RedirectToAction(nameof(Index));
    }

    // POST: AdminProducts/DeleteImage/5
    [HttpPost]
    public async Task<IActionResult> DeleteImage(int id)
    {
        var image = await _context.ProductImages.FindAsync(id);
        if (image != null)
        {
            DeleteImageFile(image.ImageUrl);
            _context.ProductImages.Remove(image);
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Xóa ảnh thành công!" });
        }

        return Json(new { success = false, message = "Không tìm thấy ảnh!" });
    }

    // POST: AdminProducts/SetPrimaryImage/5
    [HttpPost]
    public async Task<IActionResult> SetPrimaryImage(int id)
    {
        var image = await _context.ProductImages
            .Include(i => i.Product)
            .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(i => i.Id == id);

        if (image != null)
        {
            // Reset all images to non-primary
            foreach (var img in image.Product.Images)
            {
                img.IsPrimary = false;
            }

            // Set this image as primary
            image.IsPrimary = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = "Đặt ảnh chính thành công!" });
        }

        return Json(new { success = false, message = "Không tìm thấy ảnh!" });
    }

    private bool ProductExists(int id)
    {
        return _context.Products.Any(e => e.Id == id);
    }

    private async Task SaveProductImages(int productId, List<IFormFile> imageFiles)
    {
        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "products");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        foreach (var file in imageFiles)
        {
            if (file.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var productImage = new ProductImage
                {
                    ProductId = productId,
                    ImageUrl = $"/uploads/products/{fileName}",
                    FileName = file.FileName,
                    IsPrimary = false
                };

                _context.ProductImages.Add(productImage);
            }
        }

        await _context.SaveChangesAsync();
    }

    private void DeleteImageFile(string imageUrl)
    {
        if (!string.IsNullOrEmpty(imageUrl))
        {
            var filePath = Path.Combine(_environment.WebRootPath, imageUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}