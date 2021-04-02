﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using User_Service.Enums;

namespace User_Service.Models.FromFrontend
{
    public class User
    {
        [Required]
        public string Username { get; set; }
        [Required]
        public string Password { get; set; }
        public string NewPassword { get; set; }
        [Required]
        public string About { get; set; }
        [Required]
        public string Email { get; set; }
        [Required]
        public Gender Gender { get; set; }
        [Required]
        public AccountRole AccountRole { get; set; }
        [Required]
        public DateTime BirthDate { get; set; }
        public List<UserHobby> Hobbies { get; set; }
        public List<FavoriteArtist> FavoriteArtists { get; set; }
    }
}
