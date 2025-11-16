using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SAProject.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index() // ← THÊM async và Task<IActionResult>
        {
            var auditLogs = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Select(a => new AuditLogViewModel
                {
                    Id = a.Id,
                    UserId = a.UserId,
                    UserName = a.UserName ?? "N/A",
                    Action = a.Action,
                    ControllerName = a.ControllerName ?? "N/A",
                    ActionName = a.ActionName ?? "N/A",
                    RequestPath = a.RequestPath ?? "N/A",
                    IpAddress = a.IpAddress ?? "N/A",
                    Timestamp = a.Timestamp
                })
                .ToListAsync();

            return View(auditLogs);
        }
        public class AuditLogViewModel
        {
            public int Id { get; set; }
            public string UserId { get; set; }
            public string UserName { get; set; }
            public string Action { get; set; }
            public string ControllerName { get; set; }
            public string ActionName { get; set; }
            public string RequestPath { get; set; }
            public string IpAddress { get; set; }
            public DateTime Timestamp { get; set; }
        }


        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var auditLog = await _context.AuditLogs
                .FirstOrDefaultAsync(a => a.Id == id);

            if (auditLog == null)
            {
                return NotFound();
            }

            return View(auditLog);
        }
    }
}