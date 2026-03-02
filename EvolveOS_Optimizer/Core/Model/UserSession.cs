// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

namespace EvolveOS_Optimizer.Core.Model
{
    public static class UserSession
    {
        public static string? Username { get; set; }
        public static string? UserType { get; set; }
        public static bool IsAuthenticated { get; set; }

        private static ImageSource? _profileImage;

        public static ImageSource? ProfileImage
        {
            get => _profileImage;
            set
            {
                _profileImage = value;
                ProfileImageChanged?.Invoke(value);
            }
        }

        public static event Action<ImageSource?>? ProfileImageChanged;

        public static void Clear()
        {
            Username = string.Empty;
            UserType = "Guest";
            IsAuthenticated = false;

            ProfileImage = null;
        }
    }
}