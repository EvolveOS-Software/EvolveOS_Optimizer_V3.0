// Copyright (c) 2026 EvolveOS Software
//
// Licensed under the MIT License. 
// See the LICENSE file in the project root for more information.

using EvolveOS_Optimizer.Core.Base;

namespace EvolveOS_Optimizer.Core.Model
{
    public class UserAccount : ObservableObject
    {
        private string? _id;
        public string? Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        private string? _username;
        public string? Username
        {
            get => _username;
            set => SetProperty(ref _username, value);
        }

        private string? _email;
        public string? Email
        {
            get => _email;
            set => SetProperty(ref _email, value);
        }

        private string? _firstName;
        public string? FirstName
        {
            get => _firstName;
            set => SetProperty(ref _firstName, value);
        }

        private string? _lastName;
        public string? LastName
        {
            get => _lastName;
            set => SetProperty(ref _lastName, value);
        }

        private string? _dateCreated;
        public string? DateCreated
        {
            get => _dateCreated;
            set => SetProperty(ref _dateCreated, value);
        }

        private string? _userType;
        public string? UserType
        {
            get => _userType;
            set => SetProperty(ref _userType, value);
        }

        private byte[]? _rawImage;
        public byte[]? RawImage
        {
            get => _rawImage;
            set => SetProperty(ref _rawImage, value);
        }

        private BitmapImage? _profileImage;
        public BitmapImage? ProfileImage
        {
            get => _profileImage;
            set => SetProperty(ref _profileImage, value);
        }
    }
}