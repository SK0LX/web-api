using AutoMapper;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NUnit.Framework.Constraints;
using WebApi.MinimalApi.Domain;
using WebApi.MinimalApi.Models;

namespace WebApi.MinimalApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : Controller
{
    private IUserRepository userRepository;
    private IMapper mapper;
    private LinkGenerator linkGenerator;

    public UsersController(IUserRepository userRepository, IMapper mapper, LinkGenerator linkGenerator)
    {
        this.userRepository = userRepository;
        this.linkGenerator = linkGenerator;
        this.mapper = mapper;
    }

    [HttpGet("{userId}", Name = nameof(GetUserById))]
    [HttpHead("{userId}")]
    [Produces("application/json", "application/xml")]
    public ActionResult<UserDto> GetUserById([FromRoute] Guid userId)
    {
        var user = userRepository.FindById(userId);
        
        if (user == null)
        {
            return NotFound();
        }
        
        if (HttpMethods.IsHead(Request.Method))
        {
            Response.ContentType = "application/json; charset=utf-8";
            return Ok();
        }
        
        return Ok(mapper.Map<UserDto>(user));
    }

    [HttpPost]
    [Produces("application/json", "application/xml")]
    public IActionResult CreateUser([FromBody] PersonDTO user)
    {
        if (user == null)
        {
            return BadRequest();
        }        

        if (string.IsNullOrEmpty(user.Login) || !user.Login.All(char.IsLetterOrDigit))
        {
            ModelState.AddModelError("Login", "Invalid login format");
            return UnprocessableEntity(ModelState);
        }
        
        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }
        
        var userEntity = mapper.Map<UserEntity>(user);
        var insertedUser = userRepository.Insert(userEntity);

        return CreatedAtRoute(
        nameof(GetUserById),
         new { userId = insertedUser.Id },
         insertedUser.Id);
    }
 
    [HttpPut("{userId}")]
    public IActionResult UpdateUser([FromRoute] string userId, [FromBody] UpdateDto? user)
    {
        if (!Guid.TryParse(userId, out var userGuid) || user == null)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var userEntity = new UserEntity(userGuid)
        {
            Login = user.Login,
            FirstName = user.FirstName,
            LastName = user.LastName
        };

        try
        {
            userRepository.UpdateOrInsert(userEntity, out var isInserted);
            if (isInserted)
            {
                return CreatedAtRoute(
                    nameof(GetUserById),
                    new { userId = userEntity.Id }, 
                    userEntity.Id);
            }
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NoContent();
        }
    }

    [HttpPatch("{userId}")]
    public IActionResult PatchUser([FromRoute] string userId, [FromBody] JsonPatchDocument<UpdateDto> patchDoc)
    {

        if (patchDoc == null)
        {
            return BadRequest();
        }
        if (!Guid.TryParse(userId, out var userGuid) || patchDoc == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return UnprocessableEntity(ModelState);
        }

        var user =  userRepository.FindById(userGuid);
        if (user == null)
        {
            return NotFound();
        }
   
        var userDto = mapper.Map<UpdateDto>(user);
        patchDoc.ApplyTo(userDto, ModelState);
        
        if (!TryValidateModel(userDto))
        { 
            return UnprocessableEntity(ModelState);
        }
        
        try
        {
            userRepository.UpdateOrInsert(user, out var isInserted);
            if (isInserted)
            {
                return CreatedAtRoute(
                    nameof(GetUserById),
                    new { userId = user.Id }, 
                    user.Id);
            }
            return NoContent();
        }
        catch (InvalidOperationException)
        {
            return NoContent();
        }
    }

    [HttpDelete("{userId}")]
    public IActionResult DeleteUser([FromRoute] string userId)
    {
        if (!Guid.TryParse(userId, out var userGuid))
        {
            return NotFound();
        }

        var userEntity = userRepository.FindById(userGuid);
        if (userEntity == null)
        {
            return NotFound();
        }

        userRepository.Delete(userGuid);

        return NoContent();
    }


    [HttpGet(Name = nameof(GetUsers))]
    public IActionResult GetUsers([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
    {
        pageNumber = pageNumber < 1 ? 1 : pageNumber;
        pageSize = pageSize < 1 ? 1 : pageSize > 20 ? 20 : pageSize;
        
        var userEntities = userRepository.GetPage(pageNumber, pageSize);
        var users = mapper.Map<IEnumerable<UserDto>>(userEntities);

        var totalCount = userEntities.TotalCount;
        var totalPages = (int) Math.Ceiling(totalCount / (double)pageSize);
        var previousPage = pageNumber > 1 
            ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageNumber = pageNumber - 1, pageSize}) 
            : null;
        var nextPageLink = pageNumber < totalPages
            ? linkGenerator.GetUriByRouteValues(HttpContext, nameof(GetUsers), new { pageNumber = pageNumber + 1, pageSize })
            : null;
        
        var paginationHeader = new
        {
            previousPageLink = previousPage,
            nextPageLink = nextPageLink,
            totalCount = totalCount,
            pageSize = pageSize,
            currentPage = pageNumber,
            totalPages = totalPages,
        };
        Response.Headers.Add("X-Pagination", JsonConvert.SerializeObject(paginationHeader));
        
        return Ok(users);
    }

    [HttpOptions]
    public IActionResult Options()
    {
        Response.Headers.Add("Allow", "POST, GET, OPTIONS");
        return Ok();
    } 
}