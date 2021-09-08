using DevStore.Identity.API.Data;
using DevStore.Identity.API.Extensions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NetDevPack.Security.Jwt;
using NetDevPack.Security.Jwt.Store.EntityFrameworkCore;
using NetDevPack.Security.PasswordHasher.Core;

namespace DevStore.Identity.API.Configuration
{
    public static class IdentityConfig
    {
        public static IServiceCollection AddIdentityConfiguration(this IServiceCollection services,
            IConfiguration configuration)
        {
            var appSettingsSection = configuration.GetSection("AppTokenDevStoreSettings");
            services.Configure<AppTokenDevStorettings>(appSettingsSection);
            services.AddDataProtection().SetApplicationName("DevStore.Identity");
            services.AddJwksManager(options => options.Jws = JwsAlgorithm.ES256).PersistKeysToDatabaseStore<ApplicationDbContext>();

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

            services.AddDefaultIdentity<IdentityUser>(o =>
                {
                    o.Password.RequireDigit = false;
                    o.Password.RequireLowercase = false;
                    o.Password.RequireNonAlphanumeric = false;
                    o.Password.RequireUppercase = false;
                    o.Password.RequiredUniqueChars = 0;
                    o.Password.RequiredLength = 8;
                })
                .AddRoles<IdentityRole>()
                //.AddErrorDescriber<IdentityMensagensPortugues>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddDefaultTokenProviders();

            services.UpgradePasswordSecurity()
                .WithStrenghten(PasswordHasherStrenght.Moderate)
                .UseArgon2<IdentityUser>();

            return services;
        }
    }
}