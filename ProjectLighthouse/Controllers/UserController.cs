using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProjectLighthouse.Types;

namespace ProjectLighthouse.Controllers {
    [ApiController]
    [Route("LITTLEBIGPLANETPS3_XML/")]
    [Produces("text/xml")]
    public class UserController : ControllerBase {
        [HttpGet("user/{username}")]
        public async Task<IActionResult> GetUser(string username) {
            await using Database database = new();
            
            User user = await database.Users.FirstOrDefaultAsync(u => u.Username == username);

            if(user == null) return this.NotFound();
            return this.Ok(user.Serialize());
        }

        [HttpPost("user/{username}")]
        public async Task<IActionResult> CreateUser(string username) {
            await new Database().CreateUser(username);
            return await GetUser(username);
        }

        [HttpPost("updateUser")]
        public async Task<IActionResult> UpdateUser() {
            await using Database database = new();
            User user = await database.Users.Where(u => u.Username == "jvyden").FirstOrDefaultAsync();

            if(user == null) return this.BadRequest();

            XmlReaderSettings settings = new() {
                Async = true // this is apparently not default
            };

            bool locationChanged = false;

            // this is an absolute mess, but necessary because LBP only sends what changed
            //
            // example for changing profile card location:
            // <updateUser>
            //     <location>
            //         <x>1234</x>
            //         <y>1234</y>
            //     </location>
            // </updateUser>
            //
            // example for changing biography:
            // <updateUser>
            //     <biography>biography stuff</biography>
            // </updateUser>
            //
            // if you find a way to make it not stupid feel free to replace this
            using(XmlReader reader = XmlReader.Create(Request.Body, settings)) {
                List<string> path = new(); // you can think of this as a file path in the XML, like <updateUser> -> <location> -> <x>
                while(await reader.ReadAsync()) {
                    switch(reader.NodeType) {
                        case XmlNodeType.Element:
                            path.Add(reader.Name);
                            break;
                        case XmlNodeType.Text:
                            switch(path[1]) {
                                case "biography": {
                                    user.Biography = await reader.GetValueAsync();
                                    break;
                                }
                                case "location": {
                                    locationChanged = true; // if we're here then we're probably about to change the location.
                                    // ReSharper disable once ConvertIfStatementToSwitchStatement
                                    if(path[2] == "x") {
                                        user.Location.X = Convert.ToInt32(await reader.GetValueAsync()); // GetValue only returns a string, i guess we just hope its a number lol
                                    } else if(path[2] == "y") {
                                        user.Location.Y = Convert.ToInt32(await reader.GetValueAsync());
                                    }
                                    break;
                                }
                                case "icon": {
                                    user.IconHash = await reader.GetValueAsync();
                                    break;
                                }
                            }
                            break;
                        case XmlNodeType.EndElement:
                            path.RemoveAt(path.Count - 1);
                            break;
                    }
                }
            }
            
            // the way location on a user card works is stupid and will not save with the way below as-is, so we do the following:
            if(locationChanged) { // only modify the database if we modify here
                Location l = await database.Locations.Where(l => l.Id == user.LocationId).FirstOrDefaultAsync(); // find the location in the database again

                // set the location in the database to the one we modified above
                l.X = user.Location.X;
                l.Y = user.Location.Y;
                
                // now both are in sync, and will update in the database.
            }
            
            if(database.ChangeTracker.HasChanges()) await database.SaveChangesAsync(); // save the user to the database if we changed anything
            return this.Ok();
        }

        [HttpPost("match")]
        [Produces("text/json")]
        public IActionResult Match() {
            return this.Ok("[{\"StatusCode\":200}]");
        }
    }
}