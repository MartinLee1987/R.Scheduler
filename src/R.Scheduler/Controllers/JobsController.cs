﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Web;
using System.Web.Http;
using Common.Logging;
using Quartz;
using R.Scheduler.Contracts.JobTypes;
using R.Scheduler.Contracts.Model;
using R.Scheduler.Core;
using R.Scheduler.Interfaces;
using R.Scheduler.Persistance;
using StructureMap;

namespace R.Scheduler.Controllers
{
    [SchedulerAuthorize(AppSettingRoles = "Roles", AppSettingUsers = "Users")]
    public class JobsController : ApiController
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISchedulerCore _schedulerCore;

        public JobsController()
        {
            _schedulerCore = ObjectFactory.GetInstance<ISchedulerCore>();
        }

        /// <summary>
        /// Get all the jobs regardless of the job type
        /// </summary>
        /// <returns></returns>
        [AcceptVerbs("GET")]
        [Route("api/jobs")]
        [SchedulerAuthorize(AppSettingRoles = "Read.Roles", AppSettingUsers = "Read.Users") ]
        public IEnumerable<BaseJob> Get()
        {
            Logger.Debug("Entered JobsController.Get().");

            var authorizedJobGroups = PermissionsHelper.GetAuthorizedJobGroups();

            IDictionary<IJobDetail, Guid> jobDetailsMap;

            try
            {
                jobDetailsMap = _schedulerCore.GetJobDetails(authorizedJobGroups);
            }
            catch (Exception ex)
            {
                Logger.Error(ex);
                throw;
            }

            return jobDetailsMap.Select(mapItem =>
                                                    new BaseJob
                                                    {
                                                        Id = mapItem.Value,
                                                        JobName = mapItem.Key.Key.Name,
                                                        JobGroup = mapItem.Key.Key.Group,
                                                        JobType = mapItem.Key.JobType.Name,
                                                        SchedulerName = _schedulerCore.SchedulerName,
                                                        Description = mapItem.Key.Description
                                                    }).ToList();

        }

        /// <summary>
        /// Get the job specified by id
        /// </summary>
        /// <returns></returns>
        [AcceptVerbs("GET")]
        [Route("api/jobs/{id}")]
        [SchedulerAuthorize(AppSettingRoles = "Read.Roles", AppSettingUsers = "Read.Users")]
        public BaseJob Get(Guid id)
        {
            Logger.Debug("Entered JobsController.Get().");

            var authorizedJobGroups = PermissionsHelper.GetAuthorizedJobGroups().ToList();

            IJobDetail jobDetail;

            try
            {
                jobDetail = _schedulerCore.GetJobDetail(id);
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format("Error getting JobDetail: {0}", ex.Message));
                return null;
            }

            if ((authorizedJobGroups.Any() && jobDetail != null) || jobDetail != null && (authorizedJobGroups.Contains(jobDetail.Key.Group) || authorizedJobGroups.Contains("*")))
            {
                return new BaseJob
                {
                    Id = id,
                    JobName = jobDetail.Key.Name,
                    JobGroup = jobDetail.Key.Group,
                    JobType = jobDetail.JobType.Name,
                    SchedulerName = _schedulerCore.SchedulerName,
                    Description = jobDetail.Description
                };
            }
            throw new HttpResponseException(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Schedules a temporary job for an immediate execution
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/jobs/{id}")]
        [SchedulerAuthorize(AppSettingRoles = "Execute.Roles", AppSettingUsers = "Execute.Users")]
        public QueryResponse Execute(Guid id)
        {
            Logger.DebugFormat("Entered JobsController.Execute(). id = {0}", id);

            var response = new QueryResponse { Valid = true, Id = id };

            var authorizedJobGroups = PermissionsHelper.GetAuthorizedJobGroups();

            IJobDetail jobDetail;

            try
            {
                jobDetail = _schedulerCore.GetJobDetail(id);
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format("Error getting JobDetail: {0}", ex.Message));
                return null;
            }

            if (jobDetail != null && (authorizedJobGroups.Contains(jobDetail.Key.Group) || authorizedJobGroups.Contains("*")))
            {

                try
                {
                    _schedulerCore.ExecuteJob(id);
                }
                catch (Exception ex)
                {
                    response.Valid = false;
                    response.Errors = new List<Error>
                    {
                        new Error
                        {
                            Code = "ErrorExecutingJob",
                            Type = "Server",
                            Message = string.Format("Error: {0}", ex.Message)
                        }
                    };
                }

                return response;
            }
            throw new HttpResponseException(HttpStatusCode.Unauthorized);
        }

        /// <summary>
        /// Remove job and all associated triggers.
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        [AcceptVerbs("DELETE")]
        [Route("api/jobs/{id}")]
        [SchedulerAuthorize(AppSettingRoles = "Delete.Roles", AppSettingUsers = "Delete.Users")]
        public QueryResponse Delete(Guid id)
        {
            Logger.DebugFormat("Entered JobsController.Delete(). id = {0}", id);

            var response = new QueryResponse { Valid = true, Id = id };

            var authorizedJobGroups = PermissionsHelper.GetAuthorizedJobGroups().ToList();

            IJobDetail jobDetail;

            try
            {
                jobDetail = _schedulerCore.GetJobDetail(id);
            }
            catch (Exception ex)
            {
                Logger.Debug(string.Format("Error getting JobDetail: {0}", ex.Message));
                return null;
            }

            if (jobDetail != null && (authorizedJobGroups.Contains(jobDetail.Key.Group) || authorizedJobGroups.Contains("*")))
            {

                try
                {
                    _schedulerCore.RemoveJob(id);
                }
                catch (Exception ex)
                {
                    string type = "Server";
                    if (ex is ArgumentException)
                    {
                        type = "Sender";
                    }

                    response.Valid = false;
                    response.Errors = new List<Error>
                    {
                        new Error
                        {
                            Code = "ErrorRemovingJob",
                            Type = type,
                            Message = string.Format("Error removing job {0}.", ex.Message)
                        }
                    };
                }

                return response;
            }
            throw new HttpResponseException(HttpStatusCode.Unauthorized);
        }
    }
}
