using FluentValidation;
using p48_lessons_asp.Models;
using p48_lessons_asp.Models;

namespace p48_lessons_asp.Models.ProductRequestModels;

public class CreateProductRequestValidator : AbstractValidator<CreateProductImageRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Product name is required")
            .MaximumLength(200).WithMessage("Product name is too long");

        RuleFor(x => x.Price)
            .GreaterThan(0).WithMessage("Price must be greater than 0");
    }
}