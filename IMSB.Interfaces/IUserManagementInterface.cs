using IMSB.Dto.Models;

namespace IMSB.Interfaces
{
    /// <summary>
    /// User Management screen: District Officers and State Admin Officers, kept in the logins table with Role = one of
    /// <see cref="UserTypes.All"/>. Only those rows are listed, changed or deleted here (an admin, staff or inventory login
    /// id is not found). Refusals carry a shared resource key.
    /// </summary>
    public interface IUserManagementInterface
    {
        /// <summary>Every officer login, by user name.</summary>
        Task<List<ManagedUserRow>> ListAsync();

        /// <summary>
        /// Saves a validated form with a hashed password; refused when any login already has the user name ("ValUserNameTaken")
        /// or the email address ("ValEmailExists"), ignoring case.
        /// </summary>
        Task<BackendResult> AddAsync(UserFormDto form);

        /// <summary>An empty password keeps the current one; refused like <see cref="AddAsync"/> when another login has the user name or email.</summary>
        Task<BackendResult> UpdateAsync(int id, UserFormDto form);

        Task<BackendResult> DeleteAsync(int id);
    }
}
