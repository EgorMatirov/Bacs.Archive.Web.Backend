using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Bacs.Archive.Web.Backend.Contexts;
using Bacs.Archive.Web.Backend.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;

namespace Bacs.Archive.Web.Backend.Controllers
{
    [Route("api/[controller]")]
    public class TokensController : Controller
    {
        private static readonly HttpClient HttpClient = new HttpClient();
        private readonly IConfiguration _configuration;
        private readonly UsersDbContext _dbContext;

        public TokensController(IConfiguration configuration, UsersDbContext dbContext)
        {
            _configuration = configuration;
            _dbContext = dbContext;
        }

        public class Credentials
        {
            public string Code;
        }
        
        // POST
        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Credentials request)
        {
            if (string.IsNullOrEmpty(request?.Code)) return BadRequest("No auth code is provided");

            var accessToken = await ObtainAccessToken(request.Code);
            var email = (await GetEmail(accessToken)).ToLower();

            var user = await GetOrCreateUser(email);

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Id),
                new Claim(ClaimTypes.Role, user.Role.ToString()), 
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["SecurityKey"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                _configuration["SecurityDomain"],
                _configuration["SecurityDomain"],
                claims,
                expires: DateTime.Now.AddMinutes(int.Parse(_configuration["TokenExpirationTime"])),
                signingCredentials: creds);

            return Ok(new
            {
                token = new JwtSecurityTokenHandler().WriteToken(token)
            });
        }

        private async Task<User> GetOrCreateUser(string email)
        {
            var user = await _dbContext.Users.SingleOrDefaultAsync(x => x.Id == email);
            if (user != null) return user;
            
            user = new User
            {
                Id = email,
                Role = Entities.User.UserRole.Write
            };
            _dbContext.Add(user);
            await _dbContext.SaveChangesAsync();

            return user;
        }

        private static async Task<string> GetEmail(string accessToken)
        {
            var response = await HttpClient.GetAsync(
                $"https://www.googleapis.com/plus/v1/people/me?access_token={accessToken}");

            dynamic responseObject = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());

            return responseObject.emails[0].value.ToString();
        }

        private async Task<string> ObtainAccessToken(string authCode)
        {
            var response = await HttpClient.PostAsync("https://www.googleapis.com/oauth2/v4/token",
                new FormUrlEncodedContent(
                    new[]
                    {
                        new KeyValuePair<string, string>("code", authCode),
                        new KeyValuePair<string, string>("client_id", _configuration["GoogleClientId"]),
                        new KeyValuePair<string, string>("client_secret", _configuration["GoogleClientSecret"]),
                        new KeyValuePair<string, string>("redirect_uri", _configuration["GoogleClientRedirect"]),
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    }));

            dynamic responseObject = JsonConvert.DeserializeObject(await response.Content.ReadAsStringAsync());

            return responseObject.access_token.ToString();
        }
    }
}