using System.Data;
using Classes;
using gaseous_signature_parser.models.RomSignatureObject;
using hasheous_server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace hasheous_server.Controllers.v1_0
{
    [ApiController]
    [Route("api/v{version:apiVersion}/[controller]/")]
    [ApiVersion("1.0")]
    [Authorize]
    public class CompaniesController : ControllerBase
    {
        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [AllowAnonymous]
        [Route("")]
        public async Task<IActionResult> CompaniesList()
        {
            hasheous_server.Classes.Companys companys = new Classes.Companys();

            return Ok(companys.GetCompanies());
        }

        [MapToApiVersion("1.0")]
        [HttpGet]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [AllowAnonymous]
        [Route("{Id}")]
        public async Task<IActionResult> GetCompany(long Id)
        {
            hasheous_server.Classes.Companys companys = new Classes.Companys();

            Models.CompanyItem? company = companys.GetCompany(Id);

            if (company == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(company);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public async Task<IActionResult> NewCompany(Models.CompanyItemModel model)
        {
            hasheous_server.Classes.Companys companys = new Classes.Companys();

            Models.CompanyItem? company = companys.NewCompany(model);

            if (company == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(company);
            }
        }

        [MapToApiVersion("1.0")]
        [HttpPut]
        [Authorize(Roles = "Admin")]
        [Route("{Id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> EditCompany(long Id, Models.CompanyItemModel model)
        {
            hasheous_server.Classes.Companys companys = new Classes.Companys();

            Models.CompanyItem? company = companys.EditCompany(Id, model);

            if (company == null)
            {
                return NotFound();
            }
            else
            {
                return Ok(company);
            }
        }
    }
}