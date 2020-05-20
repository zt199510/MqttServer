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
        /// ���캯��
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
                var xmlPath = Path.Combine(basePath, "MqttServer.xml");//������Ǹո����õ�xml�ļ���
                c.IncludeXmlComments(xmlPath);
                var xmlModelPath = Path.Combine(basePath, "Blog.Core.Model.xml");//�������Model���xml�ļ���
                c.IncludeXmlComments(xmlModelPath);

              
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Description = "���¿�����������ͷ����Ҫ���Jwt��ȨToken��Bearer Token",
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

            #region ������Ȩ��
            //#region 1�����ڽ�ɫ��API��Ȩ 

            //// 1����Ȩ��������ܼ򵥣�����ʲô����������
            //// �������÷���ֻ��Ҫ��API���controller�ϱߣ��������Լ��ɣ�ע�⣬ֻ���ǽ�ɫ��:
            //// [Authorize(Roles = "Admin")]

            //// 2����֤����Ȼ�����±ߵ�configure������м������:app.UseMiddleware<JwtTokenAuth>();��������������޷���֤����ʱ�䣬���������Ҫ��֤����ʱ�䣬������Ҫ�±ߵĵ����ַ������ٷ���֤

            //#endregion

            //#region 2�����ڲ��Ե���Ȩ���򵥰棩

            //// 1����Ȩ����������ϱߵ�����ͬ�����ô����ǲ�����controller�У�д��� roles ��
            //// Ȼ����ôд [Authorize(Policy = "Admin")]
            services.AddAuthorization(options =>
            {
                
                options.AddPolicy("Client", policy => policy.RequireRole("Client").Build());
                options.AddPolicy("Admin", policy => policy.RequireRole("Admin").Build());
                options.AddPolicy("SystemOrAdmin", policy => policy.RequireRole("Admins", "System"));
            });


            //// 2����֤����Ȼ�����±ߵ�configure������м������:app.UseMiddleware<JwtTokenAuth>();��������������޷���֤����ʱ�䣬���������Ҫ��֤����ʱ�䣬������Ҫ�±ߵĵ����ַ������ٷ���֤
            //#endregion
            //#endregion

            //#region ����֤��
            ////��ȡ�����ļ�
            var audienceConfig = Configuration.GetSection("Audience");
            var symmetricKeyAsBase64 = audienceConfig["Secret"];
            var keyByteArray = Encoding.ASCII.GetBytes(symmetricKeyAsBase64);
            var signingKey = new SymmetricSecurityKey(keyByteArray);


            ////2.1����֤��
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
                     ValidIssuer = audienceConfig["Issuer"],//������
                     ValidateAudience = true,
                     ValidAudience = audienceConfig["Audience"],//������
                     ValidateLifetime = true,
                     ClockSkew = TimeSpan.Zero,
                     RequireExpirationTime = true,
                 };
             });
            #endregion

            #region Mqtt����
            string hostIp = Configuration["MqttOption:HostIp"];//IP��ַ
            int hostPort = int.Parse(Configuration["MqttOption:HostPort"]);//�˿ں�
            int timeout = int.Parse(Configuration["MqttOption:Timeout"]);//��ʱʱ��
            string username = Configuration["MqttOption:UserName"];//�û���
            string password = Configuration["MqttOption:Password"];//����
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
            //����ע��
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

            app.UseRouting();//·������

            app.UseAuthorization();

           

            #region mqtt����������

            app.UseMqttEndpoint();
            app.UseMqttServer(server =>
            {
                server.ApplicationMessageReceived += ServerOnApplicationMessageReceived;
                //���������¼�
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
                //����ֹͣ�¼�
                server.Stopped += (sender, args) =>
                {

                };

                //�ͻ��������¼�
                server.ClientConnected += (sender, args) =>
                {
                    var clientId = args.ClientId;
                    Console.WriteLine($"�пͻ�����");
                };
                //�ͻ��˶Ͽ��¼�
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
