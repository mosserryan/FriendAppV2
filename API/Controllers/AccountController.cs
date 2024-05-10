using System.Security.Cryptography;
using System.Text;
using API.Controllers;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers;

public class AccountController : BaseApiController
{

    private readonly DataContext context;
    private readonly ITokenService _tokenService;


    public AccountController(DataContext context, ITokenService tokenService)
    {
        this.context = context;
        _tokenService = tokenService;
    }

    [HttpPost("register")] //POST: api/account/register
    public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
    {

        if (await UserExists(registerDto.Username)) return BadRequest("Usnername is taken");

        using var hmca = new HMACSHA512(); // "using" allows us to dispose of it manaully rather than waiting for GC to do it for us. Same as running hmca.Dispose() at the end.

        var user = new AppUser 
        {
            UserName = registerDto.Username.ToLower(),
            PasswordHash = hmca.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password)),
            PasswordSalt = hmca.Key
        };

        this.context.Users.Add(user);
        await this.context.SaveChangesAsync();

        return new UserDto
        {
            UserName = user.UserName,
            Token = _tokenService.CreateToken(user)
        };
    }

    [HttpPost("login")]
    public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
    {
        var user = await this.context.Users.FirstOrDefaultAsync(x => x.UserName == loginDto.UserName);

        if(user == null) return Unauthorized("invalid username");

        using var hmca = new HMACSHA512(user.PasswordSalt);

        var computeHash = hmca.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));

        for (int i = 0; i < computeHash.Length; i++)
        {
            if (computeHash[i] != user.PasswordHash[i]) return Unauthorized("invalid password");
        }

        return new UserDto
        {
            UserName = user.UserName,
            Token = _tokenService.CreateToken(user)
        };
    }

    private async Task<bool> UserExists(string username)
    {
        return await this.context.Users.AnyAsync(user => user.UserName == username.ToLower());
    }

}
