using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SAProject.Data;
using SAProject.Models;
using System.Diagnostics;

namespace SAProject.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Products.Include(c => c.Images).ToListAsync());
    }
    public async Task<IActionResult> Details(int id)
    {
        return View(await _context.Products.Include(c => c.Images).FirstOrDefaultAsync(f => f.Id == id));
    }
    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
