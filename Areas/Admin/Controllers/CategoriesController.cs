using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
public class CategoriesController : Controller
{
    private readonly NeondbContext _context;
    private readonly CategoryServer _categoryService;
    private readonly Image _ImageServes;

    public CategoriesController(NeondbContext context, CategoryServer categoryService, Image imageServes)
    {
        _context = context;
        _categoryService = categoryService;
        _ImageServes = imageServes;
    }

    public IActionResult Index()
    {
        var model = new ViewModels
        {
            Categories = _context.Categories.ToList(),
                Products = _context.Products.ToList()
        };
        
        return View(model);
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Create(CategoryVW categoryVW)
    {
        categoryVW.Id = await _categoryService.NextCounter();

        string? imageUrl = await _ImageServes.UploadImage(categoryVW.ImageFile, "categories");

        var model = new Category
        {
            Id = categoryVW.Id,
            Name = categoryVW.Name,
            Description = categoryVW.Description,
            Imageurl = imageUrl != null ? "images/categories/" +imageUrl : "images/categories/category_8428362.png",
        };


        await _context.Categories.AddAsync(model);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
    
    public IActionResult Edit(int id)
    {
        var category = _context.Categories.FirstOrDefault(c => c.Id == id);
        if (category == null)
        {
            return NotFound();
        }
        CategoryVW categoryVW = new CategoryVW
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description,
            Existingimage = category.Imageurl
        };

        return View(categoryVW);
    }
    [HttpPost]
    public async Task<IActionResult> Edit(CategoryVW categoryVW)
    {
        var category = await _categoryService.GetCategoryByID(categoryVW.Id);
        if (category==null) return NotFound();
        string? fileName = categoryVW.Existingimage;
        if (categoryVW.ImageFile !=null)
        {
            fileName = await _ImageServes.UpdateImage(categoryVW.ImageFile,"categories", categoryVW.Existingimage);
            category.Imageurl = fileName != null ? "/images/categories/" + fileName : "images/categories/category_8428362.png";
        }
        category.Name = categoryVW.Name;
        category.Description = categoryVW.Description;
        category.Imageurl = fileName;

        _context.Update(category);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));

    }

    [HttpPost]
    public IActionResult Delete(int id)
    {
        var category = _context.Categories.FirstOrDefault(c => c.Id == id);
        if (category != null)
        {
            // لا نحذف التصنيف إذا كانت هناك منتجات مرتبطة به
            var hasProducts = _context.Products.Any(p => p.Categoryid == id);
            if (hasProducts)
            {
                TempData["Error"] = "لا يمكن حذف التصنيف لأنه يحتوي على منتجات";
                return RedirectToAction(nameof(Index));
            }
            if (!string.IsNullOrEmpty(category.Imageurl))
            {
                if (category.Imageurl != "images/categories/category_8428362.png")
                {
                    var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot",
             "images", "categories", Path.GetFileName(category.Imageurl));
                    if (System.IO.File.Exists(imagePath))
                    {
                        System.IO.File.Delete(imagePath);
                    }
                }
            }
            _context.Categories.Remove(category);
            _context.SaveChanges();

        }
        return RedirectToAction(nameof(Index));
    }
}
