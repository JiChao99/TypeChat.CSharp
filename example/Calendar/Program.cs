using Calendar;
using TypeChat;

ITypeChatJsonTranslator<CalendarActions> typeChat = new TypeChatJsonTranslator<CalendarActions>("");

var httpClient = new HttpClient();
var result = await typeChat.Translate(httpClient, "add event");