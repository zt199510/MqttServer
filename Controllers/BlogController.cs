using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Threading.Tasks;
using Blog.Core.Model;
using IdentityServer4.EntityFramework.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MqttServer.AutoHelper;
using static MqttServer.AutoHelper.JWTHelper;

namespace MqttServer.Controllers
{
    [ApiController]
  
    [Route("api/[controller]")]
    
    public class BlogController : ControllerBase
    {
        private SignInManager<IdentityUser> _signManager;
        private UserManager<IdentityUser> _userManager;

        public BlogController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signManager)
        {
            _userManager = userManager;
            _signManager = signManager;
        }
        /// <summary>
        /// Post
        /// </summary>
        /// <param name="love"></param>
        [HttpPost]
        public void Post(Love love)
        {
        }
        /// <summary>
        /// 登录接口：随便输入字符，获取token，然后添加 Authoritarian
        /// </summary>
        /// <param name="name"></param>
        /// <param name="pass"></param>
        /// <returns></returns>
        [HttpGet]
        public async Task<object> GetJWTToken(string name, string pass)
        {
            string jwtStr = string.Empty;
            bool suc = false;
            //这里就是用户登陆以后，通过数据库去调取数据，分配权限的操作
            //这里直接写死了


            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(pass))
            {
                return new JsonResult(new
                {
                    Status = false,
                    message = "用户名或密码不能为空"
                });
            }
            var user = new IdentityUser { UserName = name, Id=Guid.NewGuid().ToString()};
            var count = await _userManager.FindByIdAsync(user.Id);
            if (count==null)
            {
                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    await _signManager.SignInAsync(user, false);
                    TokenModelJWT tokenModel = new TokenModelJWT();
                    tokenModel.Uid = long.Parse(user.Id);
                    tokenModel.Role = "Admin";
                    jwtStr = JWTHelper.IssueJWT(tokenModel);
                    suc = true;
                    return Ok(new
                    {
                        success = suc,
                        token = jwtStr
                    });
                }
            }
           
            return new JsonResult(new
            {
                Status = false,
                message = "用户已经存在"
            });

        }

    }
}
