using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CaroOnline.Winforms
{
    public static class AuthService
    {
        public const bool UseFake = true;

        public static async Task<(bool ok, string error)> LoginAsync(string endpoint, string username, string password)
        {
            if (UseFake)
            {
                await Task.Delay(300);
                return (true, "");
            }
            throw new NotImplementedException();
        }

        public static async Task<(bool ok, string error)> RegisterAsync(string endpoint, string username, string password)
        {
            if (UseFake)
            {
                await Task.Delay(300);
                return (true, "");
            }
            throw new NotImplementedException();
        }
    }
}