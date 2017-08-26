using LineBotApplication.Models;
using LineMessagingAPISDK;
using LineMessagingAPISDK.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;

namespace safeprojectname.Controllers
{
    public class LineMessagesController : ApiController
    {
        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post(HttpRequestMessage request)
        {
            if (!await VaridateSignature(request))
                return Request.CreateResponse(HttpStatusCode.BadRequest);

            Activity activity = JsonConvert.DeserializeObject<Activity>
                (await request.Content.ReadAsStringAsync());

            // Line may send multiple events in one message, so need to handle them all.
            foreach (Event lineEvent in activity.Events)
            {
                LineMessageHandler handler = new LineMessageHandler(lineEvent);

                Profile profile = await handler.GetProfile(lineEvent.Source.UserId);

                switch (lineEvent.Type)
                {
                    case EventType.Beacon:
                        await handler.HandleBeaconEvent();
                        break;
                    case EventType.Follow:
                        await handler.HandleFollowEvent();
                        break;
                    case EventType.Join:
                        await handler.HandleJoinEvent();
                        break;
                    case EventType.Leave:
                        await handler.HandleLeaveEvent();
                        break;
                    case EventType.Message:
                        Message message = JsonConvert.DeserializeObject<Message>(lineEvent.Message.ToString());
                        switch (message.Type)
                        {
                            case MessageType.Text:
                                await handler.HandleTextMessage();
                                break;
                            case MessageType.Audio:
                            case MessageType.Image:
                            case MessageType.Video:
                                await handler.HandleMediaMessage();
                                break;
                            case MessageType.Sticker:
                                await handler.HandleStickerMessage();
                                break;
                            case MessageType.Location:
                                await handler.HandleLocationMessage();
                                break;
                        }
                        break;
                    case EventType.Postback:
                        await handler.HandlePostbackEvent();
                        break;
                    case EventType.Unfollow:
                        await handler.HandleUnfollowEvent();
                        break;
                }
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task<bool> VaridateSignature(HttpRequestMessage request)
        {
            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ConfigurationManager.AppSettings["ChannelSecret"].ToString()));
            var computeHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(await request.Content.ReadAsStringAsync()));
            var contentHash = Convert.ToBase64String(computeHash);
            var headerHash = Request.Headers.GetValues("X-Line-Signature").First();

            return contentHash == headerHash;
        }
    }

    public class LineMessageHandler
    {
        private Event lineEvent;
        private LineClient lineClient = new LineClient(ConfigurationManager.AppSettings["ChannelToken"].ToString());

        public LineMessageHandler(Event lineEvent)
        {
            this.lineEvent = lineEvent;
        }

        public async Task HandleBeaconEvent()
        {
        }

        public async Task HandleFollowEvent()
        {
        }

        public async Task HandleJoinEvent()
        {
        }

        public async Task HandleLeaveEvent()
        {
        }

        public async Task HandlePostbackEvent()
        {
            var replyMessage = new TextMessage(lineEvent.Postback.Data);
            await Reply(replyMessage);
        }

        public async Task HandleUnfollowEvent()
        {
        }

        public async Task<Profile> GetProfile(string mid)
        {
            return await lineClient.GetProfile(mid);
        }

        public async Task HandleTextMessage()
        {
            var textMessage = JsonConvert.DeserializeObject<TextMessage>(lineEvent.Message.ToString());
            Message replyMessage = null;
            var sleep = false;
            if (textMessage.Text == "sleep")
                sleep = true;
            else if (textMessage.Text == "wakeup")
                sleep = false;

            if (sleep) return;

            var luisres = await GetLuisResponseAsync(textMessage.Text);
            if (luisres.entities.Count(x => x.type == "Category") > 0)
            {
                var locasmadata = await GetLocaSmaResponseAsync(luisres);
                var locandcat = await GetLocationAndCategoryAsync(luisres, locasmadata);
                var message = MakeMessage(locandcat);
                replyMessage = new TextMessage(message);
            }
            await Reply(replyMessage);
        }

        public async Task HandleMediaMessage()
        {
            Message message = JsonConvert.DeserializeObject<Message>(lineEvent.Message.ToString());
            // Get media from Line server.
            Media media = await lineClient.GetContent(message.Id);
            Message replyMessage = null;

            // Reply Image 
            switch (message.Type)
            {
                case MessageType.Image:
                case MessageType.Video:
                case MessageType.Audio:
                    replyMessage = new ImageMessage("https://github.com/apple-touch-icon.png", "https://github.com/apple-touch-icon.png");
                    break;
            }

            await Reply(replyMessage);
        }

        public async Task HandleStickerMessage()
        {
            //https://devdocs.line.me/files/sticker_list.pdf
            var stickerMessage = JsonConvert.DeserializeObject<StickerMessage>(lineEvent.Message.ToString());
            var replyMessage = new StickerMessage("1", "1");
            await Reply(replyMessage);
        }

        public async Task HandleLocationMessage()
        {
            var locationMessage = JsonConvert.DeserializeObject<LocationMessage>(lineEvent.Message.ToString());
            LocationMessage replyMessage = new LocationMessage(
                locationMessage.Title,
                locationMessage.Address,
                locationMessage.Latitude,
                locationMessage.Longitude);
            await Reply(replyMessage);
        }

        private async Task Reply(Message replyMessage)
        {
            try
            {
                await lineClient.ReplyToActivityAsync(lineEvent.CreateReply(message: replyMessage));
            }
            catch
            {
                await lineClient.PushAsync(lineEvent.CreatePush(message: replyMessage));
            }
        }

        private async Task<LuisResponse> GetLuisResponseAsync(string text)
        {
            var url = "LUISアプリのURLとクエリ" + HttpUtility.UrlEncode(text);
            var textdata = await CallRestAPIAsync(url);
            var result = JsonConvert.DeserializeObject<LuisResponse>(textdata);
            return result;
        }

        private async Task<string> CallRestAPIAsync(string url)
        {
            var wc = new WebClient();
            var response = await wc.DownloadDataTaskAsync(url);
            var enc = System.Text.Encoding.GetEncoding("utf-8");
            return enc.GetString(response);
        }

        private async Task<LocationAndCategory> GetLocationAndCategoryAsync(LuisResponse luisres, List<LocaSmaData> locasmadata)
        {
            var location = luisres.entities.Find(x => x.type == "Location")?.entity;
            var categoryname = locasmadata[0].items[0].name;
            var categoryid = locasmadata[0].items[0].id;
            double lat = 0.0;
            double lon = 0.0;
            if (location != null)
            {
                var url = "Google Geocoding APIのURLおよびクエリ" + location;
                var text = await CallRestAPIAsync(url);
                var tmp = JsonConvert.DeserializeObject<GeoData>(text);
                if (tmp.results.Count != 0)
                {
                    lat = tmp.results[0].geometry.location.lat;
                    lon = tmp.results[0].geometry.location.lng;
                }
            }
            return new LocationAndCategory()
            {
                CategoryId = categoryid,
                CategoryName = categoryname,
                Location = location,
                Lat = lat,
                Lon = lon
            };
        }

        private string MakeMessage(LocationAndCategory locandcat)
        {
            var message = "";

            if (locandcat.Location != null && !locandcat.Location.Contains("近く"))
            {
                message = $"{locandcat.Location}で{locandcat.CategoryName}をお探しですか？" +
                    $"https://www.locationsmart.org/map.html?id={locandcat.CategoryId}" +
                    $"&lat={locandcat.Lat}&lon={locandcat.Lon}";
            }
            else
            {
                message = $"{locandcat.CategoryName}をお探しですか？" +
                    $"https://www.locationsmart.org/map.html?id={locandcat.CategoryId}";
            }
            return message;
        }
    }
}
