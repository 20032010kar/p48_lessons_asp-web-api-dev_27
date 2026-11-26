using System.Security.Cryptography;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using p48_lessons_asp.Data;
using p48_lessons_asp.Data.Entity;
using p48_lessons_asp.DTO.User;
using p48_lessons_asp.Extensions;
using p48_lessons_asp.Models.AuthRequestModels;
using p48_lessons_asp.Models.UserRequestModels;
using p48_lessons_asp.Services.Kdf;
using p48_lessons_asp.Services.Token;
using p48_lessons_asp.DTO.AuthDto;
using Superpower.Model;

namespace p48_lessons_asp.Controllers
{

    public record RefreshRequest(string RefreshToken);

    public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
    {
        public RefreshRequestValidator()
        {
            RuleFor(x => x.RefreshToken)
                .NotEmpty().WithMessage("Refresh token is required");
        }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DataContext _db;
        private readonly IMapper _mapper;
        
        private readonly ITokenService _tokenService;
        private readonly IKdfService _kdfService;
        private readonly ILogger<UserController> _logger;
        private readonly IValidator<LoginRequest> _loginValidator;

        private readonly IValidator<CreateUserRequest> _registerValidator;
        private readonly IValidator<RefreshRequest> _refreshValidator;
        public AuthController(DataContext db,IMapper mapper, ITokenService tokenService, IKdfService kdfService, ILogger<UserController> logger, IValidator<LoginRequest> loginValidator,IValidator<CreateUserRequest> registerValidator,IValidator<RefreshRequest> refreshValidator)
        {
            _db = db;
            _tokenService = tokenService;
            _kdfService = kdfService;
            _logger = logger;
            _loginValidator = loginValidator;
            _registerValidator = registerValidator;
            _refreshValidator = refreshValidator;

        }

        [HttpPost("register")]
        public async Task<IActionResult> Register ([FromBody] CreateUserRequest model)
        {
            var validationResult = _registerValidator.GetErrors(model);
            if (validationResult != null)
            {
                return new ObjectResult(new
                {
                    StatusCode = 400,
                    Message = "Validation error",
                    Errors = validationResult
                })
                { StatusCode = 400 };
            }

            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var passwordHash = _kdfService.GetDerivedKey(model.Password, salt);

            var user = new User
            {
                Id = Guid.NewGuid(),
                Login = model.Login,
                Email = model.Email,
                PasswordSalt = salt,
                PasswordHash = passwordHash,
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();


            try
            {
                var tokenPairData = await _tokenService.CreateSessionAsync(user);
                return new ObjectResult(new
                {
                    StatusCode = 200,
                    Message = "Ok",
                    Data = new AuthDto
                    {
                        User = _mapper.Map<UserDto>(user),
                        Tokens = tokenPairData 
                    }
                })
                { StatusCode = 200 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token service is fault");
                return new ObjectResult(new
                {
                    StatusCode = 503,
                    Message = "Service is unavaliable",
                })
                { StatusCode = 503 };
            }
        }









        [HttpPost]
        public async Task<IActionResult> Login([FromBody] LoginRequest model)
        {
            var validationResult = _loginValidator.GetErrors(model);
            if (validationResult != null)
            {
                return new ObjectResult(new
                {
                    StatusCode = 400,
                    Message = "Validation error",
                    Errors = validationResult
                })
                { StatusCode = 400 };
            }

            var user = await _db.Users.FirstOrDefaultAsync(u => u.Login == model.Login && u.DeletedAt == null);
            if (user == null)
            {
                return new ObjectResult(new
                {
                    StatusCode = 404,
                    Message = "User not found",
                })
                { StatusCode = 404 };
            }

            if (user.PasswordHash != _kdfService.GetDerivedKey(model.Password, user.PasswordSalt))
            {
                return new ObjectResult(new
                {
                    StatusCode = 400,
                    Message = "Password wrong",
                })
                { StatusCode = 400 };
            }

            try
            {
                var tokenPairData = await _tokenService.CreateSessionAsync(user);
                return new ObjectResult(new
                {
                    StatusCode = 200,
                    Message = "Ok",
                    Data = new AuthDto
                    {
                        User = _mapper.Map<UserDto>(user),
                        Tokens = tokenPairData
                    }
                })
                { StatusCode = 200 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token service is fault");
                return new ObjectResult(new
                {
                    StatusCode = 503,
                    Message = "Service is unavaliable",
                })
                { StatusCode = 503 };
            }
        }

        [HttpPost]
        [Route("refresh")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshRequest model)
        {

            var validationResult = _refreshValidator.GetErrors(model);
            if (validationResult != null)
            {
                return new ObjectResult(new
                {
                    StatusCode = 400,
                    Message = "Validation error",
                    Errors = validationResult
                })
                { StatusCode = 400 };
            }
            try
            {
                var tokens = await _tokenService.RefreshSessionAsync(model.RefreshToken);
                if (tokens == null)
                {
                    return new ObjectResult(new
                    {
                        StatusCode = 401,
                        Message = "Refresh token rejected or invalid",
                    })
                    { StatusCode = 401 };
                }

                return new ObjectResult(new
                {
                    StatusCode = 200,
                    Message = "Ok",
                    Data = tokens
                })
                { StatusCode = 200 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token service is fault");
                return new ObjectResult(new
                {
                    StatusCode = 503,
                    Message = "Service is unavaliable",
                })
                { StatusCode = 503 };
            }

        }

        [Authorize]
        [HttpPost]
        [Route("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var accessToken = await HttpContext.GetTokenAsync("access_token");
                if (!string.IsNullOrEmpty(accessToken))
                {
                    await _tokenService.DeactivateSessionAsync(accessToken);
                }

                return new ObjectResult(new
                {
                    StatusCode = 200,
                    Message = "Session closed",
                })
                { StatusCode = 200 };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Token service is fault");
                return new ObjectResult(new
                {
                    StatusCode = 503,
                    Message = "Service is unavaliable",
                })
                { StatusCode = 503 };
            }
        }

    }
}
