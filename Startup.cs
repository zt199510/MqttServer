using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace MqttServer
{
    public class Startup
    {
        private const string AllowOrigins = "_myAllowSpecificOrigins";
        private string connectionString= "Data source=d:/mydb.db";

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="configuration"></param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        /// <summary>
        ///  https://blog.csdn.net/lordwish/article/details/86708777
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();


            #region Identity
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connectionString));
            services.AddIdentity<IdentityUser, IdentityRole>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();
            var builder = services.AddIdentityServer()
                .AddAspNetIdentity<IdentityUser>()
                .AddConfigurationStore(options =>
                {
                    options
                    .ConfigureDbContext = b =>
                    b.UseSqlite(connectionString, sql => sql.MigrationsAssembly("MqttServer"));
                })
                .AddOperationalStore(options =>
                {
                    options.ConfigureDbContext = b =>
                      b.UseSqlite(connectionString, sql => sql.MigrationsAssembly("MqttServer"));
                });

            #endregion
            #region Swagger

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Myapi", Version = "v1" });
                var basePath = AppContext.BaseDirectory;
                var xmlPath = Path.Combine(basePath, "MqttServer.xml");//这个就是刚刚配置的xml文件名
                c.IncludeXmlComments(xmlPath);
                var xmlModelPath = Path.Combine(basePath, "Blog.Core.Model.xml");//这个就是Model层的xml文件名
                c.IncludeXmlComments(xmlModelPath);

              
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "在下框中输入请求头中需要添加Jwt授权Token：Bearer Token",
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.ApiKey,
                     BearerFormat = "JWT",
                    Scheme = "Bearer"
                });
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                    {
                        {
                            new OpenApiSecurityScheme{
                                Reference = new OpenApiReference {
                                            Type = ReferenceType.SecurityScheme,
                                            Id = "Bearer"}
                           },new string[] { }
                        }
                    });

            });
            #endregion

            #region 【简单授权】
            //#region 1、基于角色的API授权 

            //// 1【授权】、这个很简单，其他什么都不用做，
            //// 无需配置服务，只需要在API层的controller上边，增加特性即可，注意，只能是角色的:
            //// [Authorize(Roles = "Admin")]

            //// 2【认证】、然后在下边的configure里，配置中间件即可:app.UseMiddleware<JwtTokenAuth>();但是这个方法，无法验证过期时间，所以如果需要验证过期时间，还是需要下边的第三种方法，官方认证

            //#endregion

            //#region 2、基于策略的授权（简单版）

            //// 1【授权】、这个和上边的异曲同工，好处就是不用在controller中，写多个 roles 。
            //// 然后这么写 [Authorize(Policy = "Admin")]
            services.AddAuthorization(options =>
            {
                
                options.AddPolicy("Client", policy => policy.RequireRole("Client").Build());
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin").Build());
                options.AddPolicy("SystemOrAdmin", policy => policy.RequireRole("Admins", "System"));
            });


            //// 2【认证】、然后在下边的configure里，配置中间件即可:app.UseMiddleware<JwtTokenAuth>();但是这个方法，无法验证过期时间，所以如果需要验证过期时间，还是需要下边的第三种方法，官方认证
            //#endregion
            //#endregion

            //#region 【认证】
            ////读取配置文件
            var audienceConfig = Configuration.GetSection("Audience");
            var symmetricKeyAsBase64 = audienceConfig["Secret"];
            var keyByteArray = Encoding.ASCII.GetBytes(symmetricKeyAsBase64);
            var signingKey = new SymmetricSecurityKey(keyByteArray);


            ////2.1【认证】
            services.AddAuthentication(x =>
            {
                x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
             .AddJwtBearer(o =>
             {
                 o.RequireHttpsMetadata = false;
                 o.SaveToken = true;
                 o.TokenValidationParameters = new TokenValidationParameters
                 {
                     ValidateIssuerSigningKey = true,
                     IssuerSigningKey = signingKey,
                     ValidateIssuer = true,
                     ValidIssuer = audienceConfig["Issuer"],//发行人
                     ValidateAudience = true,
                     ValidAudience = audienceConfig["Audience"],//订阅人
                     ValidateLifetime = true,
                     ClockSkew = TimeSpan.Zero,
                     RequireExpirationTime = true,
                 };
             });
            #endregion

            #region Mqtt配置
            string hostIp = Configuration["MqttOption:HostIp"];//IP地址
            int hostPort = int.Parse(Configuration["MqttOption:HostPort"]);//端口号
            int timeout = int.Parse(Configuration["MqttOption:Timeout"]);//超时时间
            string username = Configuration["MqttOption:UserName"];//用户名
            string password = Configuration["MqttOption:Password"];//密码
            services.AddCors(options =>
            {
                options.AddPolicy(AllowOrigins, builder =>
                {
                    builder.WithOrigins($"ws://localhost:{hostPort}/mqtt")
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowAnyOrigin();
                });
            });
            var optionBuilder = new MqttServerOptionsBuilder()
                 .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Parse(hostIp))
                 .WithDefaultEndpointPort(hostPort)
                .WithDefaultCommunicationTimeout(TimeSpan.FromMilliseconds(timeout))
                .WithConnectionValidator(t =>
                {
                    if (t.Username != username || t.Password != password)
                    {
                        t.ReturnCode = MqttConnectReturnCode.ConnectionRefusedBadUsernameOrPassword;
                    }
                    t.ReturnCode = MqttConnectReturnCode.ConnectionAccepted;

                });
            var option = optionBuilder.Build();
            //服务注入
            services.AddHostedMqttServer(option)
               .AddMqttConnectionHandler()
               .AddMqttWebSocketServerAdapter();
            #endregion
        }

       
        
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseStaticFiles();

            app.UseHttpsRedirection();

            app.UseAuthentication();

            app.UseRouting();//路由配置

            app.UseAuthorization();

           

            #region mqtt服务器启动

            app.UseMqttEndpoint();
            app.UseMqttServer(server =>
            {
                server.ApplicationMessageReceived += ServerOnApplicationMessageReceived;
                //服务启动事件
                server.Started += async (sender, args) =>
                {
                    var msg = new MqttApplicationMessageBuilder().WithPayload("welcome to mqtt").WithTopic("start");
                    while (true)
                    {
                        try
                        {
                            await server.PublishAsync(msg.Build());
                            msg.WithPayload("you are welcome to mqtt");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e);
                        }
                        finally
                        {
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }
                    }
                };
                //服务停止事件
                server.Stopped += (sender, args) =>
                {

                };

                //客户端连接事件
                server.ClientConnected += (sender, args) =>
                {
                    var clientId = args.ClientId;
                    Console.WriteLine($"有客户连接");
                };
                //客户端断开事件
                server.ClientDisconnected += (sender, args) =>
                {
                    var clientId = args.ClientId;
                };

            });
            #endregion

            #region swagger
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "MqttServer");
               
            });

            #endregion

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }

        private void ServerOnApplicationMessageReceived(object sender, MqttApplicationMessageReceivedEventArgs e)
        {
            var payload = Encoding.UTF8.GetString(e.ApplicationMessage.Payload);
            Console.WriteLine("\nThere is a fucking message comes around!!.");
            Console.WriteLine($"+ Topic = {e.ApplicationMessage.Topic}");
            Console.WriteLine($"+ Payload = {payload}");
            Console.WriteLine($"+ QoS = {e.ApplicationMessage.QualityOfServiceLevel}");
            Console.WriteLine($"+ Retain = {e.ApplicationMessage.Retain}");
            Console.WriteLine($"+ ClientId = {e.ClientId}");
            if (!e.ApplicationMessage.Topic.Equals("pi-result")) return;
        }
    }
}
