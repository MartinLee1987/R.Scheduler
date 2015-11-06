﻿using System;
using System.Collections.Generic;
using System.Reflection;
using System.Web.Http;
using Common.Logging;
using Quartz;
using R.Scheduler.Contracts.Model;
using R.Scheduler.Interfaces;
using StructureMap;

namespace R.Scheduler.Controllers
{
    public class TriggersController : ApiController
    {
        private static readonly ILog Logger = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        readonly ISchedulerCore _schedulerCore;

        public TriggersController()
        {
            _schedulerCore = ObjectFactory.GetInstance<ISchedulerCore>();
        }

        /// <summary>
        /// Get all triggers of a specified job
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="jobGroup"></param>
        /// <returns></returns>
        [AcceptVerbs("GET")]
        [Route("api/triggers")]
        public IList<TriggerDetails> Get(string jobName, string jobGroup)
        {
            Logger.InfoFormat("Entered TriggersController.Get(). jobName = {0}, jobName = {1}", jobName, jobGroup);

            IEnumerable<ITrigger> quartzTriggers = _schedulerCore.GetTriggersOfJob(jobName, jobGroup);

            return TriggerHelper.GetTriggerDetails(quartzTriggers);
        }

        /// <summary>
        /// Get all triggers of all jobs
        /// </summary>
        /// <returns></returns>
        [AcceptVerbs("GET")]
        [Route("api/fireTimes")]
        public IList<TriggerFireTime> Get(DateTime start, DateTime end)
        {
            Logger.Info("Entered TriggersController.Get()");

            IEnumerable<TriggerFireTime> fireTimes = _schedulerCore.GetFireTimesBetween(start, end);

            return fireTimes as IList<TriggerFireTime>;
        }

        /// <summary>
        /// Schedule SimpleTrigger for a specified job
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/simpleTriggers")]
        public QueryResponse Post([FromBody]SimpleTrigger model)
        {
            Logger.InfoFormat("Entered TriggersController.Post(). Name = {0}", model.Name);

            var response = new QueryResponse { Valid = true };

            try
            {
                _schedulerCore.ScheduleTrigger(model);
            }
            catch (Exception ex)
            {
                response.Valid = false;
                response.Errors = new List<Error>
                {
                    new Error
                    {
                        Code = "ErrorSchedulingTrigger",
                        Type = "Server",
                        Message = string.Format("Error scheduling trigger {0}", ex.Message)
                    }
                };
            }

            return response;
        }

        /// <summary>
        /// Schedule CronTrigger for a specified job
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        [AcceptVerbs("POST")]
        [Route("api/cronTriggers")]
        public QueryResponse Post([FromBody] CronTrigger model)
        {
            Logger.InfoFormat("Entered TriggersController.Post(). Name = {0}", model.Name);

            var response = new QueryResponse { Valid = true };

            try
            {
                _schedulerCore.ScheduleTrigger(model);
            }
            catch (Exception ex)
            {
                string type = "Server";

                if (ex is FormatException)
                {
                    type = "Sender";
                }

                response.Valid = false;
                response.Errors = new List<Error>
                {
                    new Error
                    {
                        Code = "ErrorSchedulingTrigger",
                        Type = type,
                        Message = string.Format("Error scheduling CronTrigger {0}", ex.Message)
                    }
                };
            }

            return response;
        }

        /// <summary>
        /// Remove all triggers of a specified job
        /// </summary>
        /// <param name="jobName"></param>
        /// <param name="jobGroup"></param>
        /// <returns></returns>
        [AcceptVerbs("DELETE")]
        [Route("api/triggers/schedule")]
        public QueryResponse Unschedule(string jobName, string jobGroup)
        {
            Logger.InfoFormat("Entered TriggersController.Unschedule(). jobName = {0}, jobName = {1}", jobName, jobGroup);

            var response = new QueryResponse { Valid = true };

            try
            {
                _schedulerCore.RemoveJobTriggers(jobName, jobGroup);
            }
            catch (Exception ex)
            {
                response.Valid = false;
                response.Errors = new List<Error>
                {
                    new Error
                    {
                        Code = "ErrorUnschedulingJob",
                        Type = "Server",
                        Message = string.Format("Error: {0}", ex.Message)
                    }
                };
            }

            return response;
        }

        /// <summary>
        /// Remove specified trigger
        /// </summary>
        /// <param name="triggerName"></param>
        /// <param name="triggerGroup"></param>
        /// <returns></returns>
        [AcceptVerbs("DELETE")]
        [Route("api/triggers")]
        public QueryResponse DeleteTrigger(string triggerName, string triggerGroup)
        {
            Logger.InfoFormat("Entered TriggersController.DeleteTrigger(). triggerGroup = {0}, triggerGroup = {1}", triggerName, triggerGroup);

            var response = new QueryResponse { Valid = true };

            try
            {
                _schedulerCore.RemoveTrigger(triggerName, triggerGroup);
            }
            catch (Exception ex)
            {
                Logger.ErrorFormat("Error removing trigger {0}. {1}", triggerName, ex.Message);

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
                        Code = "ErrorRemovingTrigger",
                        Type = type,
                        Message = string.Format("Error removing trigger {0}.", triggerName)
                    }
                };
            }

            return response;
        }
    }
}
