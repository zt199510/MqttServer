using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MqttServer.Controllers
{
    [ApiController]

    [Route("api/[controller]")]

    public class AdminController : ControllerBase
    {
        /// <summary>
        /// 回复
        /// </summary>
        [HttpGet]
        [Authorize(Policy = "Admin")]
        public async Task<object>  GetLogger()
        {
            return Ok(new
            {
                success = true,
                token = "撒刁"
            });
        }
    }
}
