using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        private const string AllowOrigins = "_myAllowSpecificOrigins";
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
           
            
            #region Mqtt����
            string hostIp = Configuration["MqttOption:HostIp"];//IP��ַ
            int hostPort = int.Parse(Configuration["MqttOption:HostPort"]);//�˿ں�
            int timeout = int.Parse(Configuration["MqttOption:Timeout"]);//��ʱʱ��
            string username = Configuration["MqttOption:UserName"];//�û���
            string password = Configuration["MqttOption:Password"];//����
            services.AddCors(options => {
                options.AddPolicy(AllowOrigins, builder => {
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
                .WithConnectionValidator(t => {
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

            app.UseStaticFiles();
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
