using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Web;
using System.Web.Mvc;
using AutoMapper;
using Circulant.Core.Entities;
using Circulant.Core.Entities.History;
using Circulant.Core.ExtensionMethods;
using Circulant.Core.Features.History.UserWorkflow.Queries;
using Circulant.Core.Features.Studied.Queries;
using Circulant.Core.Features.Users.Commands;
using Circulant.Core.Features.Users.Queries;
using Circulant.Core.Features.UserStudyAccess.Commands;
using Circulant.Core.Features.UserStudyAccess.Queries;
using Circulant.Core.Features.UserSystemAccess.Commands;
using Circulant.Core.Features.UserSystemAccess.Queries;
using Circulant.Core.Features.UserWorkflow.Commands;
using Circulant.Core.Features.UserWorkflow.Queries;
using Circulant.Core.Helpers;
using Circulant.Web.AutoMapperConfig;
using Circulant.Web.ViewModels.Study;
using Circulant.Web.ViewModels.User;

namespace Circulant.Web.Controllers
{
    [AuthorizeRole]
    public class UserController : BaseController
    {
        IMapper mapper;

        public UserController()
        {
            mapper = new MapperConfiguration(mc => { 
                mc.AddProfile(new DomainToViewModelProfile()); 
            }).CreateMapper();
        }

        // GET: User
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult GetUsers(DataTableParam param)
        {
            // Initialization.
            JsonResult jsonResult;
                      
            try
            {
                param.Initialize(Request);

                // Getting all Customer data  
                var entityUsers = new GetAllUsersQuery().Handle(param, ref param);
                var viewUsers = mapper.Map<IList<UserListViewModel>>(entityUsers);

                var activeUsers = new GetAllUsersQuery().Handle().Where(r => r.UserStatus.Equals("Active"))
                           .OrderBy(r => r.Fullname).ToList();

                var pendingUsers = new GetAllUsersQuery().Handle().Where(r => r.UserStatus.Equals("Pending Approval"))
                           .OrderBy(r => r.Fullname).ToList();

                jsonResult = this.Json(new { draw = param.Draw, recordsTotal = param.TotalCount, recordsFiltered = param.FilteredCount, data = viewUsers, totalRecords = param.TotalCount, activeRecords = activeUsers.Count(), pendingRecords = pendingUsers.Count() }, JsonRequestBehavior.AllowGet);

            }
            catch (Exception)
            {
                throw;
            }
                
            return jsonResult;
        }

        // GET: User/Details/5
        public ActionResult View(int id)
        {

            AddUserViewModel model = new AddUserViewModel();

            var userDetail = new GetUserByIdQuery().Handle(id);
            var loggedInUsername = MyExtensions.GetProfileIdentifier();
            var loggedInUserDetail = new GetUserByIdQuery().HandleByUsername(loggedInUsername);
                     
            if (Request.QueryString["action"] != "" && Request.QueryString["action"] != null)
            {
                var approvarAction = "";
                var approverId = "";

                approvarAction = Request.QueryString["action"];

                if (Request.QueryString["apprId"] != "" && Request.QueryString["apprId"] != null)
                {
                    approverId = Request.QueryString["apprId"];
                }

                if (Convert.ToInt32(approverId) == loggedInUserDetail[0].UserID)
                {
                    if (userDetail[0].IsActive == "Y")
                    {
                        model.IsUserAlreadyApproved = true;
                    }

                    if (approvarAction == "accept")
                    {
                        model.IsUserApprovalByEmail = true;
                        model.UserApprovalAction = "accept";                        
                    }
                    else if (approvarAction == "reject")
                    {
                        model.IsUserApprovalByEmail = true;
                        model.UserApprovalAction = "reject";                        
                    }
                }
            } else  {
                model.IsUserApprovalByEmail = false;
                if (userDetail.Count > 0 && userDetail[0].IsActive == "Y")
                {
                    model.IsUserAlreadyApproved = true;
                }
            }

            if (userDetail.Count > 0)
            {
                var approversDetail = new GetUserByIdQuery().Handle(userDetail[0].ApprovalManagerID);

                var studyAccessDetail = new GetUserStudyByIdQuery().Handle(id);

                var systemAccessDetail = new GetUserSystemByIdQuery().Handle(id);

                model.StudiesList.Clear();

                IList<Study> entityStudy;
                entityStudy = new List<Study>();
                for (var i = 0; i < studyAccessDetail.Count; i++)
                {
                    entityStudy.Add(new Study()
                    {
                        StudyID = 0,
                        StudyName = "",
                        StudyType = "",
                        StudyStatus = "",
                        OnboardingDate = DateTime.UtcNow,
                        ApprovarUserID = 0,
                        AccessReason = "",
                        IsActive = "Y",
                        LastUpdatedBy = "SYSTEM",
                        LastUpdatedDate = DateTime.UtcNow
                    });

                    model.StudiesList.Add(mapper.Map<StudyListViewModel>(entityStudy[i]));
                }

                for (var i = 0; i < studyAccessDetail.Count; i++)
                {
                    var SAData = new GetStudyByIdQuery().Handle(studyAccessDetail[i].StudyAccessStudyID);
                    if (SAData.Count > 0)
                    {
                        model.StudiesList[i] = mapper.Map<StudyListViewModel>(SAData[0]);
                    }
                }

                for (var i = 0; i < systemAccessDetail.Count; i++)
                {
                    model.UserSystemAccessList[i].SystemAccessUserID = id;
                    model.UserSystemAccessList[i].SystemName = systemAccessDetail[i].SystemName;
                    model.UserSystemAccessList[i].SystemType = systemAccessDetail[i].SystemType;
                    model.UserSystemAccessList[i].AccessType = systemAccessDetail[i].AccessType;
                    model.UserSystemAccessList[i].AccessGiven = systemAccessDetail[i].AccessGiven;
                }

                var userWorkflowDetail = new GetUserWorkflowByUserIdQuery().Handle(id);
                if(userWorkflowDetail.Count > 0)
                {
                    model.UserWorkflowList[0].ApprovalComments = userWorkflowDetail[0].ApprovalComments;
                }

                model.UserID = userDetail[0].UserID;
                model.Fullname = userDetail[0].Fullname;
                model.Email = userDetail[0].Email;
                model.OnboardingDate = userDetail[0].OnboardingDate;
                model.UserType = userDetail[0].UserType;
                model.UserStatus = userDetail[0].UserStatus;
                model.UserRoleName = userDetail[0].UserRoleName;
                model.Department = userDetail[0].Department;
                model.AccessReason = userDetail[0].AccessReason;
                model.UserTags = userDetail[0].UserTags;
                model.VendorName = userDetail[0].VendorName;
                model.ApproverName = approversDetail[0].Fullname;
                model.ApproverType = userDetail[0].ApproverType;
                model.IsActive = userDetail[0].IsActive;

                return View(model);
            }
            return View();
        }


        // POST: User/View/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult View(int id, AddUserViewModel model)
        {
            try
            {
                // TODO: Add update workflow comment logic here

                var userDetail = new GetUserByIdQuery().Handle(id);
                var ApprovalStatus = Request.Form.GetValues("isApproval");
                var ApprovalComment = Request.Form.GetValues("comments");

                    if (userDetail.Count > 0)
                    {
                        if(ApprovalStatus[0] == "Y")
                        {
                            model.IsActive = "Y";
                            model.UserStatus = "Active";
                        } else if (ApprovalStatus[0] == "N")
                        {
                            model.IsActive = "N";
                            model.UserStatus = "Pending Approval";
                        }

                        model.LastUpdatedDate = DateTime.UtcNow;
                        var addUser = mapper.Map<User>(model);

                        UpdateUserCommand umodel = new UpdateUserCommand();

                        int resp = umodel.UpdateUserStatus(addUser, id);

                        if (resp > 0)
                        {

                        var userWorkflowDetail = new GetUserWorkflowByUserIdQuery().Handle(id);

                        if (userWorkflowDetail.Count > 0)
                        {

                            if (ApprovalStatus[0] == "Y")
                            {
                                model.UserWorkflowList[0].ApprovalStatus = "Active";
                            }
                            else if (ApprovalStatus[0] == "N")
                            {
                                model.UserWorkflowList[0].ApprovalStatus = "Pending Approval";
                            }
                            model.UserWorkflowList[0].UserID = userWorkflowDetail[0].UserID;
                            model.UserWorkflowList[0].InitiatorID = userWorkflowDetail[0].InitiatorID;
                            model.UserWorkflowList[0].ApproverID = userWorkflowDetail[0].ApproverID;
                            model.UserWorkflowList[0].ApprovalComments = ApprovalComment[0];

                            var addWorkflowData = mapper.Map<UserWorkflow>(model.UserWorkflowList[0]);
                            if (ApprovalStatus[0] == "Y")
                            {
                                addWorkflowData.IsActive = "Y";
                            }
                            else if (ApprovalStatus[0] == "N")
                            {
                                addWorkflowData.IsActive = "N";
                            }
                            addWorkflowData.LastUpdatedBy = "SYSTEM";
                            addWorkflowData.LastUpdatedDate = DateTime.UtcNow;

                            UpdateUserWorkflowCommand uwfModel = new UpdateUserWorkflowCommand();
                            int cwf = uwfModel.Handle(addWorkflowData, id);
                        }

                        return Redirect("../../User/Index");
                        }
                    }             


                return View(model);
            }
            catch (Exception ex)
            {
                TempData["msg"] = ex.Message;
                return View();
            }
        }


        // GET: User/Create
        public ActionResult Add()
        {
            AddUserViewModel model = new AddUserViewModel();
            model.UserStatus = "Pending Approval";
            model.OnboardingDate = DateTime.Now;
            return View(model);
        }

        // POST: User/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Add(AddUserViewModel model)
        {

            try
            {
                // TODO: Add insert logic here

                for(var i=0; i < model.UserSystemAccessList.Count; i++)
                {
                    model.UserSystemAccessList[i].SystemName = Request.Form.GetValues("[" + i + "].SystemName")[0];
                    model.UserSystemAccessList[i].SystemType = Request.Form.GetValues("[" + i + "].SystemType")[0];
                    model.UserSystemAccessList[i].AccessType = Request.Form.GetValues("["+i+"].AccessType")[0];
                    model.UserSystemAccessList[i].AccessGiven = Request.Form.GetValues("[" + i + "].AccessGiven")[0];
                }

                string[] linkStudyToUser = Request.Form.GetValues("linkStudyToUser");
                var ApprovalManagerID = Request.Form.GetValues("ApprovalManagerID");
                string[] tags = Request.Form.GetValues("UserTags");

                if (ModelState.IsValid)
                {
                    var approvarDetail = new GetUserByIdQuery().Handle(Convert.ToInt32(ApprovalManagerID[0]));

                    if(tags != null)
                    {
                        if (tags.Length > 0)
                        {
                            model.UserTags = "";
                            for (var i = 0; i < tags.Length; i++)
                            {
                                model.UserTags += tags[i] + ",";
                            }
                            model.UserTags = model.UserTags.TrimEnd(',');
                        }
                    }

                    model.UserName = model.Email;
                    model.IsDatabricksProcessed = 1;
                    model.IsActive = "N";
                    model.UserSystemDefined = false;
                    model.LastUpdatedBy = "SYSTEM";
                    model.LastUpdatedDate = DateTime.UtcNow;
                    model.ApprovalManagerID = Convert.ToInt32(ApprovalManagerID[0]);
                    model.ApproverType = approvarDetail[0].UserRoleName;

                    var addUser = mapper.Map<User>(model);

                    CreateUserCommand umodel = new CreateUserCommand();

                    int resp = umodel.Handle(addUser);


                    if (resp > 0)
                    {
                        var username = MyExtensions.GetProfileIdentifier();
                        var userDetail = new GetUserByIdQuery().HandleByUsername(username);

                        if (userDetail.Count > 0)
                        {
                            model.UserWorkflowList[0].UserID = resp;
                            model.UserWorkflowList[0].InitiatorID = userDetail[0].UserID;
                            model.UserWorkflowList[0].ApproverID = Convert.ToInt32(ApprovalManagerID[0]);
                            model.UserWorkflowList[0].ApprovalStatus = "Pending Approval";
                            model.UserWorkflowList[0].ApprovalComments = "";

                            var addWorkflowData = mapper.Map<UserWorkflow>(model.UserWorkflowList[0]);
                            addWorkflowData.IsActive = "N";
                            addWorkflowData.LastUpdatedBy = "SYSTEM";
                            addWorkflowData.LastUpdatedDate = DateTime.UtcNow;
                            CreateUserWorkflowCommand cwfModel = new CreateUserWorkflowCommand();
                            int cwf = cwfModel.Handle(addWorkflowData);
                        }


                        if (linkStudyToUser != null)
                        {
                            for (var i = 0; i < linkStudyToUser.Length; i++)
                            {
                                model.UserStudyAccessList[i].StudyAccessUserID = resp;
                                model.UserStudyAccessList[i].StudyAccessStudyID = Convert.ToInt32(linkStudyToUser[i]);

                                var addStudyAccess = mapper.Map<UserStudyAccess>(model.UserStudyAccessList[i]);
                                addStudyAccess.IsActive = "Y";
                                addStudyAccess.LastUpdatedBy = "SYSTEM";
                                addStudyAccess.LastUpdatedDate = DateTime.UtcNow;
                                CreateUserStudyAccessCommand cuModel = new CreateUserStudyAccessCommand();
                                int sresp = cuModel.Handle(addStudyAccess);
                            }
                        }

                        for (var i = 0; i < model.UserSystemAccessList.Count; i++)
                        {
                            model.UserSystemAccessList[i].SystemAccessUserID = resp;

                            var addSystemAccess = mapper.Map<UserSystemAccess>(model.UserSystemAccessList[i]);
                            addSystemAccess.IsActive = "Y";
                            addSystemAccess.LastUpdatedBy = "SYSTEM";
                            addSystemAccess.LastUpdatedDate = DateTime.UtcNow;

                            CreateUserSystemAccessCommand samodel = new CreateUserSystemAccessCommand();
                            int sresp = samodel.Handle(addSystemAccess);

                        }


                        SendEmailToApprove(approvarDetail[0].UserID, approvarDetail[0].Fullname, approvarDetail[0].Email, model.Fullname, resp);
                    }         

                    return Redirect("Index");
                }           

                return View(model);
            }
            catch (Exception ex)
            {
                TempData["msg"] = ex.Message;
                return View();
            }
        }       


        // GET: User/Edit/5
        public ActionResult Edit(int id)
        {
            var userDetail = new GetUserByIdQuery().Handle(id);

            AddUserViewModel model = new AddUserViewModel();
                       
            if(userDetail.Count > 0)
            {
                var approversDetail = new GetUserByIdQuery().Handle(userDetail[0].ApprovalManagerID);

                var studyAccessDetail = new GetUserStudyByIdQuery().Handle(id);

                var systemAccessDetail = new GetUserSystemByIdQuery().Handle(id);

                for (var i = 0; i < model.ApprovalUsersList.Count; i++)
                {                   
                    if (model.ApprovalUsersList[i].UserID == approversDetail[0].UserID)
                    {
                        model.ApprovalUsersList[i].IsUserApprover = "Yes";
                    }    
                    
                }

                for (var i = 0; i < model.StudiesList.Count; i++)
                {
                    for (var j = 0; j < studyAccessDetail.Count; j++)
                    {
                        var SAData = new GetStudyByIdQuery().Handle(studyAccessDetail[j].StudyAccessStudyID);
                        if (SAData.Count > 0)
                        {
                            if (model.StudiesList[i].StudyID == SAData[0].StudyID)
                            {
                                model.StudiesList[i].StudyIsSelected = "Yes";
                            }
                        }
                    }     
                }

                for (var i = 0; i < systemAccessDetail.Count; i++)
                {
                    model.UserSystemAccessList[i].SystemAccessUserID = id;
                    model.UserSystemAccessList[i].SystemName = systemAccessDetail[i].SystemName;
                    model.UserSystemAccessList[i].SystemType = systemAccessDetail[i].SystemType;
                    model.UserSystemAccessList[i].AccessType = systemAccessDetail[i].AccessType;
                    model.UserSystemAccessList[i].AccessGiven = systemAccessDetail[i].AccessGiven;
                }

                if (userDetail[0].UserTags != null)
                {

                    var split = userDetail[0].UserTags.Split(',').Select(s =>
                        new SelectListItem()
                        {
                            Text = s,
                            Value = s,
                            Selected = true
                        }
                     );
                    model.UserTagsList = split.ToList();
                }
                else
                {
                    var split = new List<SelectListItem>() {
                      new SelectListItem()
                      {
                      }
                    };
                    model.UserTagsList = split.ToList();
                }

                model.Fullname = userDetail[0].Fullname;
                model.Email = userDetail[0].Email;
                model.OnboardingDate = userDetail[0].OnboardingDate;
                model.UserType = userDetail[0].UserType;
                model.UserRoleID = userDetail[0].UserRoleID;
                model.UserRoleName = userDetail[0].UserRoleName;
                model.Department = userDetail[0].Department;
                model.AccessReason = userDetail[0].AccessReason;
                model.VendorID = userDetail[0].VendorID;
                model.VendorName = userDetail[0].VendorName;
                model.ApproverType = userDetail[0].ApproverType;
                model.IsActive = userDetail[0].IsActive;
                return View(model);
            }

            return View();
        }

        // POST: User/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(int id, AddUserViewModel model)
        {
            try
            {
                // TODO: Add update logic here
                for (var i = 0; i < model.UserSystemAccessList.Count; i++)
                {
                    model.UserSystemAccessList[i].SystemName = Request.Form.GetValues("[" + i + "].SystemName")[0];
                    model.UserSystemAccessList[i].SystemType = Request.Form.GetValues("[" + i + "].SystemType")[0];
                    model.UserSystemAccessList[i].AccessType = Request.Form.GetValues("[" + i + "].AccessType")[0];
                    model.UserSystemAccessList[i].AccessGiven = Request.Form.GetValues("[" + i + "].AccessGiven")[0];
                }

                string[] linkStudyToUser = Request.Form.GetValues("linkStudyToUser");
                var ApprovalManagerID = Request.Form.GetValues("ApprovalManagerID");
                var IsActive = Request.Form.GetValues("IsActive");
                string[] tags = Request.Form.GetValues("UserTags");

                if (ModelState.IsValid)
                {
                    var approvarDetail = new GetUserByIdQuery().Handle(Convert.ToInt32(ApprovalManagerID[0]));

                    var studyAccessDetail = new GetUserStudyByIdQuery().Handle(id);

                    var systemAccessDetail = new GetUserSystemByIdQuery().Handle(id);

                    if (tags != null)
                    {
                        if (tags.Length > 0)
                        {
                            model.UserTags = "";
                            for (var i = 0; i < tags.Length; i++)
                            {
                                model.UserTags += tags[i] + ",";
                            }
                            model.UserTags = model.UserTags.TrimEnd(',');
                        }
                    }

                    model.IsDatabricksProcessed = -1;
                    model.LastUpdatedBy = "SYSTEM";
                    model.LastUpdatedDate = DateTime.UtcNow;
                    model.ApprovalManagerID = Convert.ToInt32(ApprovalManagerID[0]);
                    model.ApproverType = approvarDetail[0].UserRoleName;

                    var addUser = mapper.Map<User>(model);

                    UpdateUserCommand umodel = new UpdateUserCommand();

                    int resp = umodel.Handle(addUser, id);

                    if (resp > 0)
                    {

                        for (var i = 0; i < studyAccessDetail.Count; i++)
                        {
                            DeleteUserStudyAccessCommand usaModel = new DeleteUserStudyAccessCommand();
                            int sresp = usaModel.Handle(studyAccessDetail[i].StudyAccessID);
                        }

                        if (linkStudyToUser != null)
                        {
                            for (var i = 0; i < linkStudyToUser.Length; i++)
                            {
                                model.UserStudyAccessList[i].StudyAccessUserID = resp;
                                model.UserStudyAccessList[i].StudyAccessStudyID = Convert.ToInt32(linkStudyToUser[i]);

                                var addStudyAccess = mapper.Map<UserStudyAccess>(model.UserStudyAccessList[i]);
                                addStudyAccess.IsActive = "Y";
                                addStudyAccess.LastUpdatedBy = "SYSTEM";
                                addStudyAccess.LastUpdatedDate = DateTime.UtcNow;
                                CreateUserStudyAccessCommand cuModel = new CreateUserStudyAccessCommand();
                                int sresp = cuModel.Handle(addStudyAccess);
                            }
                        }

                        for (var i = 0; i < systemAccessDetail.Count; i++)
                        {
                            DeleteUserSystemAccessCommand usaModel = new DeleteUserSystemAccessCommand();
                            int sresp = usaModel.Handle(systemAccessDetail[i].SystemAccessID);
                        }


                        for (var i = 0; i < model.UserSystemAccessList.Count; i++)
                        {
                            model.UserSystemAccessList[i].SystemAccessUserID = resp;

                            var addSystemAccess = mapper.Map<UserSystemAccess>(model.UserSystemAccessList[i]);
                            addSystemAccess.IsActive = "Y";
                            addSystemAccess.LastUpdatedBy = "SYSTEM";
                            addSystemAccess.LastUpdatedDate = DateTime.UtcNow;

                            CreateUserSystemAccessCommand samodel = new CreateUserSystemAccessCommand();
                            int sresp = samodel.Handle(addSystemAccess);
                        }

                     //  SendEmailToApprove(approvarDetail[0].UserID, approvarDetail[0].Fullname, approvarDetail[0].Email, model.Fullname, id);
                    }


                    return Redirect("../../User/Index");
                }


                return View(model);
            }
            catch (Exception ex)
            {
                TempData["msg"] = ex.Message;
                return View();
            }
        }

        [HttpGet]
        public virtual ActionResult _LinkedStudiesPartial(int id)
        {
            AddUserViewModel model = new AddUserViewModel();

            var studyAccessDetail = new GetUserStudyByIdQuery().Handle(id);

            model.StudiesList.Clear();

            IList<Study> entityStudy;
            entityStudy = new List<Study>();
            for (var i = 0; i < studyAccessDetail.Count; i++)
            {
                entityStudy.Add(new Study()
                {
                    StudyID = 0,
                    StudyName = "",
                    StudyType = "",
                    StudyStatus = "",
                    OnboardingDate = DateTime.UtcNow,
                    ApprovarUserID = 0,
                    AccessReason = "",
                    IsActive = "Y",
                    LastUpdatedBy = "SYSTEM",
                    LastUpdatedDate = DateTime.UtcNow
                });

                model.StudiesList.Add(mapper.Map<StudyListViewModel>(entityStudy[i]));
            }

            for (var i = 0; i < studyAccessDetail.Count; i++)
            {
                var SAData = new GetStudyByIdQuery().Handle(studyAccessDetail[i].StudyAccessStudyID);
                if (SAData.Count > 0)
                {
                    model.StudiesList[i] = mapper.Map<StudyListViewModel>(SAData[0]);
                }
            }

            return PartialView("_LinkedStudiesPartial", model);
        }

        [HttpGet]
        public virtual ActionResult _UserWorkflowCommentsPartial(int id)
        {
            AddUserViewModel model = new AddUserViewModel();

            var userWorkflowDetail = new GetUserWorkflowByUserIdQuery().Handle(id);
            var userWorkflowHistory = new GetUserWorkflowHistoryById().Handle(id);
            model.UserWorkflowList.Clear();

            if (userWorkflowDetail.Count > 0)
            {
                IList<UserWorkflowHistory> entityWorkflow;
                entityWorkflow = new List<UserWorkflowHistory>();
                for (var i = 0; i < userWorkflowHistory.Count; i++)
                {
                    entityWorkflow.Add(new UserWorkflowHistory()
                    {
                        UserWorkflowId = 0,
                        UserID = 0,
                        InitiatorID = 0,
                        ApproverID = 0,
                        ApprovalStatus = "Pending Approval",
                        ApprovalComments = "No",
                        ApprovalName = "No",
                        ApprovalRole = "No",
                        IsActive = "N",
                        LastUpdatedBy = "SYSTEM",
                        LastUpdatedDate = DateTime.UtcNow
                    });

                    model.UserWorkflowList.Add(mapper.Map<UserWorkflowViewModel>(entityWorkflow[i]));
                }
            }

            if (userWorkflowDetail.Count > 0)
            {
                if (userWorkflowDetail[0].ApprovalComments != null)
                {
                    model.UserWorkflowList[0].ApprovalStatus = userWorkflowDetail[0].ApprovalStatus;
                    model.UserWorkflowList[0].ApproverID = userWorkflowDetail[0].ApproverID;
                    model.UserWorkflowList[0].ApprovalComments = userWorkflowDetail[0].ApprovalComments;
                    model.UserWorkflowList[0].LastUpdatedDate = userWorkflowDetail[0].LastUpdatedDate;
                    var approvarDetail = new GetUserByIdQuery().Handle(userWorkflowDetail[0].ApproverID);
                    if (approvarDetail.Count > 0)
                    {

                        model.UserWorkflowList[0].ApprovalName = approvarDetail[0].Fullname;
                        model.UserWorkflowList[0].ApprovalRole = approvarDetail[0].UserRoleName;
                    }
                }
            }

            for (var i = 1; i < userWorkflowHistory.Count; i++)
            {
                if (userWorkflowHistory[i].ApprovalComments != null)
                {
                    model.UserWorkflowList[i].ApprovalStatus = userWorkflowHistory[i].ApprovalStatus;
                    model.UserWorkflowList[i].ApproverID = userWorkflowHistory[i].ApproverID;
                    model.UserWorkflowList[i].ApprovalComments = userWorkflowHistory[i].ApprovalComments;
                    model.UserWorkflowList[i].LastUpdatedDate = userWorkflowHistory[i].LastUpdatedDate;
                    var approvarDetail = new GetUserByIdQuery().Handle(userWorkflowHistory[i].ApproverID);
                    if (approvarDetail.Count > 0)
                    {

                        model.UserWorkflowList[i].ApprovalName = approvarDetail[0].Fullname;
                        model.UserWorkflowList[i].ApprovalRole = approvarDetail[0].UserRoleName;
                    }
                }
            }

            return PartialView("_UserWorkflowCommentsPartial", model);
        }


        public ActionResult SendEmailToApprove(int approverId, string approverName, string email, string newUser, int userId)
        {

            string host = Request.Url.Host.ToLower();
            string scheme = Request.Url.Scheme.ToLower();

            var urlBuilder = new System.UriBuilder(Request.Url.AbsoluteUri)
            {
                Path = Url.Action("View", "User"),
                Query = null,
            };
            string acceptUrl = urlBuilder.Uri.ToString() + "/" + userId + "?action=accept&apprId=" + approverId;
            string rejectUrl = urlBuilder.Uri.ToString() + "/" + userId + "?action=reject&apprId=" + approverId;

            MailMessage message = new MailMessage("noreply_crplus@circulants.com", email);

            string textBody = "<html><body>";           
                   textBody += "<p>Hi " + approverName + "</p>";
                   textBody += "<p>You are receiving this notification as a new user was added onto Platform CR+ and you were assigned as their approval manager. Please accept or reject the approval of the user using the below buttons.</p><br/>";
                   textBody += "<p>Added user's name: " + newUser + "</p>";
                   textBody += "<a class='form-control' target='_blank' href='" + acceptUrl + "'>Accept</a> &nbsp;&nbsp;";
                   textBody += "<a class='form-control' target='_blank' href='" + rejectUrl + "'>Reject</a>";
                   textBody += "</body></html>";

          
            message.Subject = "Platform CR+ : New User Approval";
            message.Body = textBody;
            message.BodyEncoding = Encoding.UTF8;
            message.IsBodyHtml = true;

            SmtpClient smtpClient = new SmtpClient(); //smtp

            try
            {              
                smtpClient.Send(message);
                return View();
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }
    }
}
