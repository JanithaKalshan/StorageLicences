using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace StorageLicenses.Infastructure;

public static class InfrastructureConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services,IConfiguration configuration)
    {

        // Register DbContext
        services.AddDbContext<Repositories.ApplicationDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        #region Register your infrastructure services here


        #endregion

        return services;
    }
}
