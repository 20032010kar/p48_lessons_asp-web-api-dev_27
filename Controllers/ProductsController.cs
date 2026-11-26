using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using p48_lessons_asp.Data;
using p48_lessons_asp.Data.Entity;
using p48_lessons_asp.DTO.Product;
using p48_lessons_asp.Extensions;
using p48_lessons_asp.Models;
using p48_lessons_asp.Models.ProductRequestModels;
using p48_lessons_asp.Services.Slug;
using p48_lessons_asp.Services.StorageService;

namespace p48_lessons_asp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ProductsController : ControllerBase
    {
        private readonly DataContext _dataContext;
        private readonly IValidator<CreateProductModel> _validator;
        private readonly IValidator<CreateProductRequest> _createProductRequestValidator;
        private readonly IValidator<CreateProductImageRequest> _imageValidator;
        private readonly ISlugService _slugService;
        private readonly IStorageService _storageService;
        private readonly IMapper _mapper;

        public ProductsController(
            DataContext dataContext,
            IValidator<CreateProductModel> validator,
            IValidator<CreateProductRequest> createProductRequestValidator,
            ISlugService slugService,
            IMapper mapper,
            IValidator<CreateProductImageRequest> imageValidator,
            IStorageService storageService)
        {
            _dataContext = dataContext;
            _validator = validator;
            _createProductRequestValidator = createProductRequestValidator;
            _slugService = slugService;
            _mapper = mapper;
            _imageValidator = imageValidator;
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> GetProducts()
        {
            try
            {
                var products = await _dataContext.Products.ToListAsync();
                if (products == null || products.Count == 0)
                {
                    return new ObjectResult(
                        new ResponseModel<object>
                        {
                            StatusCode = 404,
                            Message = "Products not found",
                        }
                    )
                    {
                        StatusCode = 404
                    };
                }
                return new ObjectResult(
                    new ResponseModel<List<Data.Entity.Product>>
                    {
                        StatusCode = 200,
                        Message = "Ok",
                        Data = products
                    }
                )
                { StatusCode = 200 };
            }
            catch (OperationCanceledException ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                System.Console.WriteLine($"[ERROR] Cannot get products with reason: {ex.Message} | {DateTime.Now} | ProductsController/GetProducts");
                Console.ResetColor();

                return new ObjectResult(
                   new ResponseModel<object>
                   {
                       StatusCode = StatusCodes.Status500InternalServerError,
                       Message = "Cannot get products. See server logs",
                   })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }

        [HttpPost]
        [Route("product-image")]
        public async Task<IActionResult> CreateProductImage([FromForm] CreateProductImageRequest model)
        {
            var validationResult = _imageValidator.GetErrors(model);
            if (validationResult != null)
            {
                return new ObjectResult(validationResult) { StatusCode = 400 };
            }

            var product = await _dataContext.Products.FirstOrDefaultAsync(p => p.Id == model.ProductId);
            if (product == null)
            {
                return new ObjectResult(new
                {
                    StatusCode = 404,
                    Message = "Product not found"
                })
                { StatusCode = 404 };
            }

            var imageUrl = await _storageService.SaveFileAsync(nameof(Product), product.Name, model.Image);
            if (string.IsNullOrEmpty(imageUrl))
            {
                return new ObjectResult(new
                {
                    StatusCode = 503,
                    Message = "Product image not added"
                })
                { StatusCode = 503 };
            }

            var productImage = new Data.Entity.ProductImage
            {
                Url = imageUrl,
                ProductId = product.Id,
                Order = 0
            };

            await _dataContext.ProductImages.AddAsync(productImage);
            await _dataContext.SaveChangesAsync();

            return new ObjectResult(new
            {
                StatusCode = 201,
                Message = "Created",
                Data = _mapper.Map<ProductImageDto>(productImage)
            })
            { StatusCode = 201 };
        }

        [HttpPost]
        public async Task<IActionResult> GetNewProduct([FromBody] CreateProductModel model)
        {
            var validationResult = await _validator.GetErrorsAsync(model);
            if (validationResult != null)
            {
                return new ObjectResult(validationResult) { StatusCode = 400 };
            }

            var product = new Data.Entity.Product()
            {
                Name = model.Name,
                Slug = _slugService.GetSlug(model.Name),
                CategoryId = model.CategoryId,
                Price = model.Price,
                Description = model.Description
            };

            try
            {
                await _dataContext.Products.AddAsync(product);
                await _dataContext.SaveChangesAsync();
                return new ObjectResult(new
                {
                    StatusCode = 201,
                    Message = "Created",
                    Data = _mapper.Map<ProductDto>(product)
                })
                { StatusCode = 201 };
            }
            catch (OperationCanceledException ex)
            {
                System.Console.WriteLine($"Created product error: {ex.Message}");
                return new ObjectResult(new
                {
                    StatusCode = 503,
                    Message = "Service is unavaliable",
                    Data = (object)null!
                })
                { StatusCode = 503 };
            }
        }

        [HttpPost]
        [Route("create")]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductRequest? model)
        {
            if (model == null)
            {
                return new ObjectResult(
                    new ResponseModel<object>
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                        Message = "Product data is required"
                    })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            var validationResult = await _createProductRequestValidator.GetErrorsAsync(model);
            if (validationResult != null)
            {
                return new ObjectResult(
                    new ResponseModel<object>
                    {
                        StatusCode = StatusCodes.Status400BadRequest,
                        Message = "Validation error",
                        Data = validationResult
                    })
                {
                    StatusCode = StatusCodes.Status400BadRequest
                };
            }

            try
            {
                var newProduct = new Data.Entity.Product
                {
                    Name = model.Name,
                    Slug = _slugService.GetSlug(model.Name),
                    Price = model.Price,
                    Description = model.Description
                };

                _dataContext.Products.Add(newProduct);
                await _dataContext.SaveChangesAsync();

                return new ObjectResult(new ResponseModel<Data.Entity.Product>
                {
                    StatusCode = StatusCodes.Status201Created,
                    Message = "Product successfuly created",
                    Data = newProduct
                })
                { StatusCode = StatusCodes.Status201Created };
            }
            catch (OperationCanceledException ex)
            {
                Console.ForegroundColor = ConsoleColor.DarkRed;
                System.Console.WriteLine($"[CRITICAL] Cannot create new product with reason: {ex.Message} | {DateTime.Now} | ProductsController/CreateProduct");
                Console.ResetColor();

                return new ObjectResult(
                   new ResponseModel<object>
                   {
                       StatusCode = StatusCodes.Status500InternalServerError,
                       Message = "Cannot create product. See server logs",
                   })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }
}