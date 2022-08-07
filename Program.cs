using Serilog;
using System.Data.SqlClient;
using VisaStatusApi;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Data;
using FluentValidation;
using FluentValidation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


//Set up reading appsettings
IConfiguration config = new ConfigurationBuilder()
.AddJsonFile("appsettings.json")
.AddEnvironmentVariables()
.Build();
var settings = config.GetRequiredSection("Settings").Get<Settings>();

builder.Services.Configure<Settings>(builder.Configuration.GetSection("Settings"));


//set up logger
builder.Logging.ClearProviders();
string logpath = Path.Combine(Environment.CurrentDirectory.ToString(), "logs", settings.logsetting.filename);
var logger = new LoggerConfiguration()
     .MinimumLevel.Error()
     .WriteTo.File(logpath, rollingInterval: RollingInterval.Day)
     .CreateLogger();
builder.Logging.AddSerilog(logger);

builder.Services.AddSingleton<ServiceRepository>();
builder.Services.AddFluentValidation(fv => fv.RegisterValidatorsFromAssemblyContaining<UpdateDTO>());

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();


app.MapGet("/", () => String.Format("You are missing something!!! "));



app.MapPost("/updatestatus", async ([FromServices] ServiceRepository _serviceRepository, IValidator<UpdateDTO> validator, UpdateDTO _input, HttpContext context) =>
{
    Microsoft.Extensions.Primitives.StringValues _authenticationheader;

    try
    {
        if (context.Request.Headers.TryGetValue("x-access-token", out _authenticationheader))
        {
            string token = _authenticationheader.First();
            if (!string.IsNullOrEmpty(token))
            {
                if (token.Trim() != settings.token)
                    return Results.Unauthorized();

            }
            else
                return Results.Unauthorized();
        }
        else
        {
            return Results.Unauthorized();
        }

    }
    catch
    {
        return Results.Unauthorized();
    }
    var validations = validator.Validate(_input);
    if (!validations.IsValid)
    {
        var errors = new { errors = validations.Errors.Select(x => x.ErrorMessage) };
        return Results.BadRequest(errors);

    }
    return await Task.FromResult(Results.Json(_serviceRepository.UpdateApplicationStatus(_input)));

}).Produces<WebResponse<string>>();


app.Run();


public class UpdateDTO
{
    public int reqresumeid { get; set; }
    public int reqid { get; set; }
    public int resumeid { get; set; }
    //public int userid { get; set; }
    public string status { get; set; } = String.Empty;
    public string comment { get; set; } = String.Empty;

    public string ticketnumber { get; set; } = String.Empty;


}

public class UpdateDTOValidator : AbstractValidator<UpdateDTO>
{

    public UpdateDTOValidator()
    {
        RuleFor(model => model.reqresumeid).GreaterThan(0);
        RuleFor(model => model.reqid).GreaterThan(0);
        RuleFor(model => model.resumeid).GreaterThan(0);
        RuleFor(model => model.status).NotEmpty().MinimumLength(3);
        RuleFor(model => model.ticketnumber).NotEmpty().MinimumLength(3);

    }
}

public class WebResponse<T>
{
    public int status { get; set; }
    public string message { get; set; } = String.Empty;
    public T? data { get; set; }
}

public class StageDTO
{
    public int stageid { get; set; }
    public string stagetitle { get; set; } = String.Empty;
    public int stagelevel { get; set; }
}

public class StatusDTO
{
    public int statusid { get; set; }
    public string statustitle { get; set; } = String.Empty;
    public int statuslevel { get; set; }
}



public class ServiceRepository
{
    private readonly IOptions<Settings> _appsettings;
    private readonly Microsoft.Extensions.Logging.ILogger _logger;

    public ServiceRepository(IOptions<Settings> appsettings, ILoggerFactory logger)
    {
        _logger = logger.CreateLogger("ServiceRepository");
        _appsettings = appsettings;

    }

    string getdbconnection => _appsettings.Value.isuat ? _appsettings.Value.dbuat : _appsettings.Value.dbproduction;


    public WebResponse<string> UpdateApplicationStatus(UpdateDTO _input)
    {
        WebResponse<string> _result = new WebResponse<string>
        {
            data = String.Empty,
            message = "processing",
            status = 201
        };

        try
        {
            using (var _dbConnection = new SqlConnection(getdbconnection))
            {
                string _squery = "";

                //int _modifieduserid = _input.userid > 0 ? _input.userid : _appsettings.Value.modifieduserid;
                int _modifieduserid = _appsettings.Value.modifieduserid;


                //Get Stage Details
                _squery = "select RID as stageid, Title as stagetitle, StageLevel as stagelevel   from HCM_STAGE with(nolock) where RID = @rid";
                var _stagedetails = _dbConnection.Query<StageDTO>(_squery, new { rid = _appsettings.Value.defaultstageid }).SingleOrDefault();

                //Get Status Details
                //_squery = "select RID as statusid, Title as statustitle, StatusLevel as statuslevel from HCM_STATUS with(nolock) where RID = @rid";
                _squery = "select RID as statusid, Title as statustitle, StatusLevel as statuslevel from HCM_STATUS with(nolock) where isnull(Title,'') = @title";

                var _statusdetails = _dbConnection.Query<StatusDTO>(_squery, new { title = _input.status.Trim().ToLower() }).SingleOrDefault();

                if (_stagedetails is not null && _statusdetails is not null)
                {
                    _squery = " Update HC_REQ_RESUME set StageID = @stageid, StageLevel = @stagelevel, StageTitle = @stagetitle, StatusID = @statusid, StatusLevel = @statuslevel, StatusTitle = @statustitle, ModifiedUserID = @modifieduserid, ModifiedDate = GETUTCDATE() where rid = @reqresumeid";

                    int _updatedrows = _dbConnection.Execute(_squery, new { stageid = _stagedetails.stageid, stagelevel = _stagedetails.stagelevel, stagetitle = _stagedetails.stagetitle, statusid = _statusdetails.statusid, statuslevel = _statusdetails.statuslevel, statustitle = _statusdetails.statustitle, modifieduserid = _modifieduserid, reqresumeid = _input.reqresumeid });

                    if (_updatedrows > 0)
                    {
                        _squery = " Insert into HC_REQ_RESUME_STAGE_STATUS(ReqResID, StageID, StageTitle, StageLevel, StatusID, StatusTitle, StatusLevel, UpdatedUserID, UpdatedDate, Notes,WFCHVisaID)  VALUES(@ReqResID, @StageID,@StageTitle,@StageLevel,@StatusID,@StatusTitle,@StatusLevel,@UpdatedUserID,@UpdatedDate,@Notes,@WFCHVisaID)";

                        var dp = new DynamicParameters();
                        dp.Add("@ReqResID", _input.reqresumeid);
                        dp.Add("@StageID", _stagedetails.stageid);
                        dp.Add("@StageTitle", _stagedetails.stagetitle);
                        dp.Add("@StageLevel", _stagedetails.stagelevel);
                        dp.Add("@StatusID", _statusdetails.statusid);
                        dp.Add("@StatusTitle", _statusdetails.statustitle);
                        dp.Add("@StatusLevel", _statusdetails.statuslevel);
                        dp.Add("@UpdatedUserID", _modifieduserid);
                        dp.Add("@UpdatedDate", DateTime.UtcNow);
                        dp.Add("@Notes", _input.comment);
                        dp.Add("@WFCHVisaID", _input.ticketnumber);
                        


                        int _insertedrows = _dbConnection.Execute(_squery, dp);


                        if (_insertedrows > 0)
                        {
                            _result = new WebResponse<string>
                            {
                                data = "record_updated",
                                message = "record_updated",
                                status = 200
                            };


                            //Trigger Email here

                            //Get Email Detals
                            _squery = " Select isnull((select HC_ENTITY.Name from  HC_ENTITY where OrgLevel = 1 " +
                                       " and RID in (select Top 1 HC_REQ_ORG.Orgid from HC_REQ_ORG where Orglevel = 1 and ReqId = HCR.RID)),'') as companyname, " +
                                        " isnull((select HC_ENTITY.Name from HC_ENTITY where OrgLevel = 2 " +
                                        " and RID in (select Top 1 HC_REQ_ORG.Orgid from HC_REQ_ORG where Orglevel = 2 and ReqId = HCR.RID)),'') as buname, " +
                                        " HCR.ReqTitle as reqtitle , HCR.ReqNumber as jobcode,isnull(HCRM.FirstName, '') + ' ' + isnull(HCRM.LastNAme, '') as candidatename, HCRB.CandidateNo as candidateno," +
                                        " isnull((Select Title from HCM_STAGE with(nolock) where RID = HCRR.Stageid),'') as stage," +
                                        " isnull((Select Title from HCM_STATUS with(nolock) where RID = HCRR.StatusID),'') as status," +
                                        " HCRR.ModifiedDate as statusdate," +
                                        " isnull((select HC.EmailID + ';' from HC_REQ_ALLOCATION HCRA with(nolock) " +
                                        " Inner Join HC_USer_main HC with(nolock) on HC.RID = HCRA.MemberID " +
                                        " Inner Join HC_USER HCU with(nolock) on HCU.UserID = HC.RID and HCU.RoleID = 1 " +
                                        " Where HCRA.ReqID = HCR.RID and isnull(HCRA.IsActive, 0) = 1 for xml path('')),'') as ccemail, " +
                                        " isnull((select HC.EmailID   from HC_USer_main HC with(nolock) " +
                                        " Inner Join HC_USER HCU with(nolock) on HCU.UserID = HC.RID and HCU.RoleID = 2 " +
                                        " Where HC.RID = HCR.RequesterID),'') as bccemail " +
                                        " from  HC_REQUISITIONS HCR with(nolock) " +
                                        " Inner Join HC_REQ_RESUME HCRR with(nolock) on HCRR.ReqID = HCR.RID " +
                                        " Inner Join HC_RESUME_BANK HCRB with(nolock) on HCRB.RID = HCRR.ResID " +
                                        " Inner Join HC_USER_MAIN HCRM with(nolock) on HCRM.RID = HCRB.UserID " +
                                        " Where HCRR.RID = @reqresumeid and HCRR.ResID = @resid and HCR.RID = @reqid";


                            var _emaildetails = _dbConnection.Query<MailTaglist>(_squery, new { reqresumeid = _input.reqresumeid, resid = _input.resumeid, reqid = _input.reqid }).SingleOrDefault();

                            if (_emaildetails is not null)
                                Mailer<ServiceRepository>.EmailWithParser(_appsettings, _logger, _appsettings.Value.mailsetting.to, _appsettings.Value.mailsetting.to,
                                          _appsettings.Value.mailsetting.name, "notification", _emaildetails, _emaildetails.ccemail, _emaildetails.bccemail);



                        }
                        else
                        {
                            _result = new WebResponse<string>
                            {
                                data = "log_insert_failed",
                                message = "log_insert_failed",
                                status = 501
                            };

                        }

                    }
                    else
                    {
                        _result = new WebResponse<string>
                        {
                            data = "no_record_found",
                            message = "no_record_found",
                            status = 502
                        };

                    }
                }
                else
                {
                    _result = new WebResponse<string>
                    {
                        data = "invalid_stage_update",
                        message = "invalid_stage_update",
                        status = 503
                    };
                }


            }

        }
        catch (Exception ex)
        {
            Mailer<ServiceRepository>.EmailWithParser(_appsettings, _logger, _appsettings.Value.mailsetting.to, _appsettings.Value.mailsetting.to,
                                         _appsettings.Value.mailsetting.name, "error", new MailTaglist { error = ex.Message }, "", "");

        }


        return _result;

    }

}

