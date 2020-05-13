using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.AspNetCore;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace MqttServer
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            #region Mqtt配置
            string hostIp = Configuration["MqttOption:HostIp"];//IP地址
            int hostPort = int.Parse(Configuration["MqttOption:HostPort"]);//端口号
            int timeout = int.Parse(Configuration["MqttOption:Timeout"]);//超时时间
            string username = Configuration["MqttOption:UserName"];//用户名
            string password = Configuration["MqttOption:Password"];//密码

            var optionBuilder = new MqttServerOptionsBuilder()
                 .WithDefaultEndpointBoundIPAddress(System.Net.IPAddress.Parse(hostIp))
                 .WithDefaultEndpointPort(hostPort)
                .WithDefaultCommunicationTimeout(TimeSpan.FromMilliseconds(timeout))
                .WithConnectionValidator(t => {
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

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        [Obsolete]
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseRouting();

            app.UseAuthorization();

            app.UseConnections(c => c.MapConnectionHandler<MqttConnectionHandler>("/data", options =>
            {
                options.WebSockets.SubProtocolSelector = MQTTnet.AspNetCore.ApplicationBuilderExtensions.SelectSubProtocol;
            }));

            #region mqtt服务器启动



            app.UseMqttEndpoint("/data");
            app.UseMqttServer(server =>
            {
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
                };
                //客户端断开事件
                server.ClientDisconnected += (sender, args) =>
                {
                    var clientId = args.ClientId;
                };

            });
            #endregion



            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
