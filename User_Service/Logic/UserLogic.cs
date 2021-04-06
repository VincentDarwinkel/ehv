﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using AutoMapper;
using User_Service.CustomExceptions;
using User_Service.Dal;
using User_Service.Enums;
using User_Service.Models;
using User_Service.Models.FromFrontend;

namespace User_Service.Logic
{
    public class UserLogic
    {
        private readonly SecurityLogic _securityLogic;
        private readonly IUserDal _userDal;
        private readonly IMapper _mapper;

        public UserLogic(SecurityLogic securityLogic, IUserDal userDal, IMapper mapper)
        {
            _securityLogic = securityLogic;
            _userDal = userDal;
            _mapper = mapper;
        }

        /// <summary>
        /// Saves the user in the database
        /// </summary>
        /// <param name="user">The form data the user send</param>
        public async Task Register(User user)
        {
            UserDto dbUser = await _userDal.Find(user.Username, user.Email);
            if (dbUser != null)
            {
                throw new DuplicateNameException();
            }

            user.Password = _securityLogic.HashPassword(user.Password);
            var userDto = _mapper.Map<UserDto>(user);
            userDto.DisabledUser = new DisabledUserDto
            {
                Reason = DisableReason.EmailVerificationRequired,
                UserUuid = userDto.Uuid
            };

            await _userDal.Add(userDto);
        }

        /// <returns>All users in the database</returns>
        public async Task<List<UserDto>> All()
        {
            return await _userDal.All();
        }

        /// <summary>
        /// Finds all users which match the uuid in the collection
        /// </summary>
        /// <param name="uuidCollection">The uuid collection</param>
        /// <returns>The found users, null if nothing is found</returns>
        public async Task<List<UserDto>> Find(List<Guid> uuidCollection)
        {
            return await _userDal.Find(uuidCollection);
        }

        /// <summary>
        /// Updates the user
        /// </summary>
        /// <param name="user">The new user data</param>
        /// <param name="requestingUserUuid">the uuid of the user that made the request</param>
        public async Task Update(User user, Guid requestingUserUuid)
        {
            UserDto dbUser = await _userDal.Find(requestingUserUuid);
            await ValidateUpdateData(user, dbUser);

            dbUser.Username = user.Username;
            dbUser.Email = user.Email;
            dbUser.About = user.About;
            dbUser.Hobbies = user.Hobbies;
            dbUser.FavoriteArtists = user.FavoriteArtists;

            await _userDal.Update(dbUser);
        }

        /// <summary>
        /// Checks if the updated data is valid, if not an exception is thrown
        /// </summary>
        /// <param name="user">The new data</param>
        /// <param name="dbUser">The data from the database</param>
        private async Task ValidateUpdateData(User user, UserDto dbUser)
        {
            if (dbUser == null)
            {
                throw new UnprocessableException();
            }

            if (!string.IsNullOrEmpty(user.Password) && !string.IsNullOrEmpty(user.NewPassword))
            {
                if (!_securityLogic.VerifyPassword(user.Password, dbUser.Password))
                {
                    throw new UnauthorizedAccessException();
                }

                dbUser.Password = _securityLogic.HashPassword(user.NewPassword);
            }

            if (user.Email != dbUser.Email && await _userDal.Exists(null, user.Email))
            {
                throw new DuplicateNameException();
            }

            if (user.Username != dbUser.Username && await _userDal.Exists(user.Username, null))
            {
                throw new DuplicateNameException();
            }
        }

        /// <summary>
        /// Deletes the user by uuid
        /// </summary>
        /// <param name="requestingUser">The user that made the request</param>
        /// <param name="userUuidToDeleteUuid">The uuid of the user to remove</param>
        public async Task Delete(UserDto requestingUser, Guid userUuidToDeleteUuid)
        {
            UserDto dbUserToDelete = await _userDal.Find(userUuidToDeleteUuid);
            if (dbUserToDelete == null)
            {
                throw new KeyNotFoundException();
            }

            if (requestingUser.AccountRole == AccountRole.SiteAdmin)
            {
                await _userDal.Delete(userUuidToDeleteUuid);
            }

            if (requestingUser.AccountRole == AccountRole.Admin && dbUserToDelete.AccountRole == AccountRole.User)
            {
                await _userDal.Delete(userUuidToDeleteUuid);
            }

            if (requestingUser.AccountRole == AccountRole.User && requestingUser.Uuid == userUuidToDeleteUuid)
            {
                await _userDal.Delete(userUuidToDeleteUuid);
            }

            throw new UnauthorizedAccessException();
        }
    }
}