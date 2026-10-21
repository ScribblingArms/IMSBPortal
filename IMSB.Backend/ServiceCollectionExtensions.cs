using IMSB.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace IMSB.Backend
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Registers every page backend; scoped, like the IMSBContext they share.</summary>
        public static IServiceCollection AddImsbBackend(this IServiceCollection services) => services
            .AddScoped<IAccountInterface, Account>()
            .AddScoped<IApplicationsInterface, Applications>()
            .AddScoped<IArtisansInterface, Artisans>()
            .AddScoped<IAttendanceInterface, Attendance>()
            .AddScoped<IDashboardInterface, Dashboard>()
            .AddScoped<IInventoryInterface, Inventory>()
            .AddScoped<INotificationsInterface, Notifications>()
            .AddScoped<IReportsInterface, Reports>()
            .AddScoped<ISalesInterface, Sales>()
            .AddScoped<ISearchInterface, Search>()
            .AddScoped<IStaffInterface, StaffDetails>()
            .AddScoped<IStallsInterface, Stalls>();
    }
}
