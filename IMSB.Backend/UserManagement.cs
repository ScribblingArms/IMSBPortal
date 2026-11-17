using IMSB.Dto.Models;
using IMSB.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IMSB.Backend
{
    public class UserManagement : BackendBase, IUserManagementInterface
    {
        public UserManagement(IMSBContext context) : base(context) { }

        // District Officers and State Admin Officers; admin, staff and inventory logins are out of reach of this screen
        private IQueryable<Login> ManagedUsers => _context.Logins.Where(l => UserTypes.All.Contains(l.Role));

        public async Task<List<ManagedUserRow>> ListAsync() =>
            (await ManagedUsers.AsNoTracking().OrderBy(l => l.Username).ToListAsync())
                .Select(l => new ManagedUserRow(l.Id, l.Username, l.Email, l.Mobile, l.Role, l.District, l.IsActive, l.CreatedAt, l.LastLogin))
                .ToList();

        public async Task<BackendResult> AddAsync(UserFormDto form)
        {
            var userName = form.UserName!.Trim();
            if (await UserNameTakenAsync(userName, null)) return BackendResult.Error("ValUserNameTaken");
            if (await EmailTakenAsync(form.Email!.Trim(), null)) return BackendResult.Error("ValEmailExists");

            var login = new Login { Username = userName, IsActive = true, CreatedAt = DateTime.UtcNow };
            Apply(login, form);
            login.PasswordHash = new PasswordHasher<Login>().HashPassword(login, form.Password!);
            _context.Logins.Add(login);
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> UpdateAsync(int id, UserFormDto form)
        {
            var login = await ManagedUsers.FirstOrDefaultAsync(l => l.Id == id);
            if (login == null) return BackendResult.Missing;
            var userName = form.UserName!.Trim();
            if (await UserNameTakenAsync(userName, id)) return BackendResult.Error("ValUserNameTaken");
            if (await EmailTakenAsync(form.Email!.Trim(), id)) return BackendResult.Error("ValEmailExists");

            login.Username = userName;
            Apply(login, form);
            if (!string.IsNullOrEmpty(form.Password))
                login.PasswordHash = new PasswordHasher<Login>().HashPassword(login, form.Password);
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        public async Task<BackendResult> DeleteAsync(int id)
        {
            var login = await ManagedUsers.FirstOrDefaultAsync(l => l.Id == id);
            if (login == null) return BackendResult.Missing;

            // its sessions and password reset links go with it (cascade)
            _context.Logins.Remove(login);
            await _context.SaveChangesAsync();
            return BackendResult.Done;
        }

        private static void Apply(Login login, UserFormDto form)
        {
            login.Email = form.Email!.Trim();
            login.Mobile = MobileNumbers.Normalize(form.Mobile);
            login.Role = UserTypes.All.First(t => t == form.UserType!.Trim());
            login.District = DistrictNames.Find(form.District);
        }

        // sign-in looks user names up case-insensitively, so "Ravi" and "ravi" may not both exist
        private Task<bool> UserNameTakenAsync(string userName, int? exceptId)
        {
            var lower = userName.ToLower();
            return _context.Logins.AnyAsync(l => l.Username.ToLower() == lower && l.Id != exceptId);
        }

        // sign-in also accepts the email address, so it must belong to one login only
        private Task<bool> EmailTakenAsync(string email, int? exceptId)
        {
            var lower = email.ToLower();
            return _context.Logins.AnyAsync(l => l.Email != null && l.Email.ToLower() == lower && l.Id != exceptId);
        }
    }
}
