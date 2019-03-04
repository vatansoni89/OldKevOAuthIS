using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using ImageGallery.Client.Services;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.AspNetCore.Authentication;

/*
microsoft.aspnetcore.authentication.cookies : 

It enables cookie based authentication.
Once an Identity Token has been validated and transformed to a claims identity, it can be stored in an encrypted cookie which is
then used on subsequent requests on the web app.
*/

/*
microsoft.aspnetcore.authentication.openidconnect : 

This middleware handles "creating the request to the authorization end point to toekn end point".
Possible other request.it will handle validation and so on.
Once a token has been validated, It's transformed into a claims identity which is used to sign into the application. And then the cookies middle =ware takes over on
subsquesnt request.
*/

namespace ImageGallery.Client
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.
            services.AddMvc();

            // register an IHttpContextAccessor so we can access the current
            // HttpContext in services by injecting it
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            // register an IImageGalleryHttpClient
            services.AddScoped<IImageGalleryHttpClient, ImageGalleryHttpClient>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Shared/Error");
            }

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationScheme = "Cookies"
            });

            //subject id etc. will have the real mapping.
            JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

            app.UseOpenIdConnectAuthentication(new OpenIdConnectOptions
            {
                AuthenticationScheme = "oidc",
                Authority = "https://localhost:44370/",
                RequireHttpsMetadata = true,
                ClientId = "imagegalleryclient",
                Scope = { "openid", "profile" },
                ResponseType = "code id_token",
                // CallbackPath = new PathString("...")
                // SignedOutCallbackPath = new PathString("")
                SignInScheme = "Cookies",
                SaveTokens = true,
                ClientSecret = "secret",
                GetClaimsFromUserInfoEndpoint = true,

                Events = new OpenIdConnectEvents()
                {
                    OnTokenValidated = tokenValidatedContext =>
                    {
                        var identity = tokenValidatedContext.Ticket.Principal.Identity
                            as ClaimsIdentity;

                        var subjectClaim = identity.Claims.FirstOrDefault(z => z.Type == "sub");

                        var newClaimsIdentity = new ClaimsIdentity(
                          tokenValidatedContext.Ticket.AuthenticationScheme,
                          "given_name",
                          "role");

                        newClaimsIdentity.AddClaim(subjectClaim);

                        tokenValidatedContext.Ticket = new AuthenticationTicket(
                            new ClaimsPrincipal(newClaimsIdentity),
                            tokenValidatedContext.Ticket.Properties,
                            tokenValidatedContext.Ticket.AuthenticationScheme);

                        return Task.FromResult(0);
                    },

                    OnUserInformationReceived = userInformationReceivedContext =>
                    {
                        userInformationReceivedContext.User.Remove("address");
                        return Task.FromResult(0);
                    }
                }
            });


            app.UseStaticFiles();

            app.UseMvc(routes =>
            {
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Gallery}/{action=Index}/{id?}");
            });
        }         
    }
}
