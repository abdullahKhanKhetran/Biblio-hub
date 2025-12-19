using Library_Management_System.Data;
using Library_Management_System.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Library_Management_System.Controllers
{
    [Authorize]
    public class BookRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public BookRequestsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: My Requests (User)
        public async Task<IActionResult> MyRequests()
        {
            var userId = _userManager.GetUserId(User);
            var requests = await _context.BookRequests
                .Include(r => r.Book)
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // GET: All Requests (Admin Only)
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var requests = await _context.BookRequests
                .Include(r => r.Book)
                .Include(r => r.User)
                .OrderByDescending(r => r.RequestDate)
                .ToListAsync();

            return View(requests);
        }

        // POST: Create Request
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int bookId)
        {
            var userId = _userManager.GetUserId(User);
            var book = await _context.Books.FindAsync(bookId);

            if (book == null)
            {
                return NotFound();
            }

            // Check if book is available
            if (book.AvailableQuantity <= 0)
            {
                TempData["Error"] = "This book is currently unavailable.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            // Check if user already has a pending or approved request for this book
            var existingRequest = await _context.BookRequests
                .FirstOrDefaultAsync(r => r.UserId == userId
                                       && r.BookId == bookId
                                       && (r.Status == RequestStatus.Pending || r.Status == RequestStatus.Approved));

            if (existingRequest != null)
            {
                TempData["Error"] = "You already have an active request for this book.";
                return RedirectToAction("Details", "Books", new { id = bookId });
            }

            // Create new request
            var request = new BookRequest
            {
                BookId = bookId,
                UserId = userId,
                RequestDate = DateTime.UtcNow,
                Status = RequestStatus.Pending
            };

            _context.BookRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Book request submitted successfully!";
            return RedirectToAction(nameof(MyRequests));
        }

        // POST: Approve Request (Admin Only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Approve(int id)
        {
            var request = await _context.BookRequests
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            if (request.Book.AvailableQuantity <= 0)
            {
                TempData["Error"] = "Book is no longer available.";
                return RedirectToAction(nameof(Index));
            }

            request.Status = RequestStatus.Approved;
            request.ApprovedDate = DateTime.UtcNow;
            request.DueDate = DateTime.UtcNow.AddDays(14); // 14 days loan period

            // Decrease available quantity
            request.Book.AvailableQuantity--;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Request approved successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: Reject Request (Admin Only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.BookRequests.FindAsync(id);

            if (request == null)
            {
                return NotFound();
            }

            request.Status = RequestStatus.Rejected;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Request rejected.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Mark as Returned (Admin Only)
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> MarkReturned(int id)
        {
            var request = await _context.BookRequests
                .Include(r => r.Book)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            request.Status = RequestStatus.Returned;
            request.ReturnDate = DateTime.UtcNow;

            // Increase available quantity
            request.Book.AvailableQuantity++;

            await _context.SaveChangesAsync();

            TempData["Success"] = "Book marked as returned!";
            return RedirectToAction(nameof(Index));
        }
    }
}