using IdentityModel;
using IdentityServer4.EntityFramework.Entities;
using IdentityServer4.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MqttServer
{
    public class Config
    {

        public static IEnumerable<IdentityServer4.Models.IdentityResource> GetIdentityResources()
        {
            return new List<IdentityServer4.Models.IdentityResource>
            {
                new IdentityResources.OpenId(),
                new IdentityResources.Profile(),
                new IdentityResources.Email(),
                new IdentityServer4.Models.IdentityResource("roles","角色",new List<string>{ JwtClaimTypes.Role}),
                new IdentityServer4.Models.IdentityResource("roleName","角色名",new List<string>{ "roleName"})
            };
        }

       
    }
}
