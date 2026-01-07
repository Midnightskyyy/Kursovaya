using Auth.API.Entities;
using Auth.API.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Core.Models;
using Shared.Messages.Events;
using Shared.Messages.Interfaces;
using System.ComponentModel.DataAnnotations;

namespace Auth.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class AuthController : ControllerBase
    {
        private readonly IUserRepository _userRepository;
        private readonly ITokenService _tokenService;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IMessageBusClient _messageBus;
        private readonly ILogger<AuthController> _logger;

        public AuthController(
            IUserRepository userRepository,
            ITokenService tokenService,
            IPasswordHasher<User> passwordHasher,
            IMessageBusClient messageBus,
            ILogger<AuthController> logger)
        {
            _userRepository = userRepository;
            _tokenService = tokenService;
            _passwordHasher = passwordHasher;
            _messageBus = messageBus;
            _logger = logger;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                _logger.LogInformation("Registration attempt for email: {Email}", request.Email);

                // Проверка существования пользователя
                if (await _userRepository.ExistsByEmailAsync(request.Email))
                {
                    return Conflict(ApiResponse.Fail("User with this email already exists"));
                }

                // Создание пользователя
                var user = new User
                {
                    Email = request.Email,
                    PhoneNumber = request.PhoneNumber,
                    Role = request.Role ?? "Customer"
                };

                // Хэширование пароля
                user.PasswordHash = _passwordHasher.HashPassword(user, request.Password);

                // Создание профиля
                user.Profile = new UserProfile
                {
                    FirstName = request.FirstName,
                    LastName = request.LastName
                };

                await _userRepository.AddAsync(user);

                // Публикация события
                _messageBus.Publish(new UserCreatedEvent
                {
                    UserId = user.Id,
                    Email = user.Email,
                    Role = user.Role,
                    CreatedAt = DateTime.UtcNow
                }, "user.events", "user.created");

                _logger.LogInformation("User registered successfully: {UserId}", user.Id);

                return CreatedAtAction(nameof(GetProfile),
                    new { id = user.Id },
                    ApiResponse.Ok(new { userId = user.Id }, "User registered successfully"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during registration for email: {Email}", request.Email);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var user = await _userRepository.GetByEmailAsync(request.Email);
                if (user == null)
                {
                    return Unauthorized(ApiResponse.Fail("Invalid credentials"));
                }

                var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);
                if (result != PasswordVerificationResult.Success)
                {
                    return Unauthorized(ApiResponse.Fail("Invalid credentials"));
                }

                if (!user.IsActive)
                {
                    return Unauthorized(ApiResponse.Fail("Account is deactivated"));
                }

                var accessToken = _tokenService.GenerateAccessToken(user);
                var refreshToken = _tokenService.GenerateRefreshToken();

                await _tokenService.SaveRefreshTokenAsync(user.Id, refreshToken);

                return Ok(ApiResponse.Ok(new
                {
                    AccessToken = accessToken,
                    RefreshToken = refreshToken,
                    ExpiresIn = 3600,
                    UserId = user.Id,
                    Email = user.Email,
                    Role = user.Role,
                    Name = $"{user.Profile?.FirstName} {user.Profile?.LastName}"
                }, "Login successful"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login for email: {Email}", request.Email);
                return StatusCode(500, ApiResponse.Fail("Internal server error"));
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequest request)
        {
            // Реализация обновления токена
            return Ok();
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
            await _tokenService.RevokeRefreshTokenAsync(token);
            return Ok(ApiResponse.Ok(null, "Logged out successfully"));
        }

        [HttpGet("profile/{id}")]
        [Authorize]
        public async Task<IActionResult> GetProfile(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound(ApiResponse.Fail("User not found"));

            return Ok(ApiResponse.Ok(new
            {
                user.Id,
                user.Email,
                user.PhoneNumber,
                user.Role,
                Profile = user.Profile
            }));
        }
    }

    public class RegisterRequest
    {
        [Required, EmailAddress, MaxLength(255)]
        public string Email { get; set; }

        [Required, MinLength(6)]
        public string Password { get; set; }

        [Phone]
        public string PhoneNumber { get; set; }

        [MaxLength(100)]
        public string FirstName { get; set; }

        [MaxLength(100)]
        public string LastName { get; set; }

        public string Role { get; set; }
    }

    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Password { get; set; }
    }

    public class RefreshTokenRequest
    {
        [Required]
        public string RefreshToken { get; set; }
    }
}