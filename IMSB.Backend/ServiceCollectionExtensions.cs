using IMSB.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace IMSB.Backend
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>Registers every page backend and the sign-in sessions; scoped, like the IMSBContext they share.</summary>
        public static IServiceCollection AddImsbBackend(this IServiceCollection services)
        {
            services.TryAddSingleton(TimeProvider.System);
            // uploads of the website's stall application form (Uploads:ShgPhotosPath, default App_Data/shg-photos)
            services.TryAddSingleton<ShgFileStore>();
            return services
                .AddScoped<SessionService>()
                .AddScoped<IAccountInterface, Account>()
                .AddScoped<IApplicationsInterface, Applications>()
                .AddScoped<IArtisansInterface, Artisans>()
                .AddScoped<IAttendanceInterface, Attendance>()
                .AddScoped<IDashboardInterface, Dashboard>()
                .AddScoped<IInventoryInterface, Inventory>()
                .AddScoped<ILeavesInterface, Leaves>()
                .AddScoped<INotificationsInterface, Notifications>()
                .AddScoped<IOtAssignmentsInterface, OtAssignments>()
                .AddScoped<IPayrollInterface, Payroll>()
                .AddScoped<IReportsInterface, Reports>()
                .AddScoped<ISalesInterface, Sales>()
                .AddScoped<ISearchInterface, Search>()
                .AddScoped<IShgApplicationsInterface, ShgApplications>()
                .AddScoped<IShgDetailsInterface, ShgDetails>()
                .AddScoped<IStaffInterface, StaffDetails>()
                .AddScoped<IStallsInterface, Stalls>()
                .AddScoped<IUserManagementInterface, UserManagement>();
        }
    }
}
