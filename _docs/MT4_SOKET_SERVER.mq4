#include "jAson.mqh";

#define TG_BOT_API_TOKEN "6700347540:AAHjTJ8L1N1Wmk91WOxMrW00OA04-deuA8s"
#define TG_CHANNEL_ID "@charts22"
#define TG_CHANNEL_ACTIONS "@charts33"

string data_folder = "CASHBALL";
input string symbol_suffix = ".s";
input bool debug_mode = false; // Режим отладки - только логирование без открытия ордеров

// Добавляем функции из OBSERVER.mq4

// Функция для проверки является ли символ золотом
bool is_gold(string symbol)
{
   if (symbol == "XAUUSD" || symbol == "XAUUSD.s" || symbol == "XAUEUR.s" || symbol == "XAUEUR")
   {
      return true;
   }
   return false;
}


CJAVal http_get(string url, bool is_print=false, bool is_ret_json=false)
{
   string cookie=NULL,result_headers;
   char post[],result[];
   int res;
   string headers="User-Agent: Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";
   
   ResetLastError();
   res=WebRequest("GET",url,headers, 5000,post,result,result_headers);   

   if(is_print)
   {
      Print("HTTP GET :", url);
      Print("HTTP GET RESULT :", CharArrayToString(result));
   }
   
   if(res==-1)
   {
      Print("Error in WebRequest.", " Error code  =" + GetLastError());
   }

   // CharArrayToString(result);

   CJAVal json;
   string str_result = CharArrayToString(result);

   if(is_ret_json && StringLen(str_result))
   {
      bool is_ok = json.Deserialize(str_result);

      if(is_ok) {
         return json.FindKey("data");
      }
   }
   
   return json;
}

void send_tg_message(string text)
{
    string url = "https://api.telegram.org/bot"+TG_BOT_API_TOKEN+"/sendMessage?chat_id="+TG_CHANNEL_ID+"&text=" + text;
    http_get(url);
}

void send_tg_trade_event_log(string text)
{
    string url = "https://api.telegram.org/bot"+TG_BOT_API_TOKEN+"/sendMessage?chat_id="+TG_CHANNEL_ACTIONS+"&text=" + text;
    http_get(url);
}

// Функция для расчета размера лота
double calc_lots_size(string symbol, double current_price, double sl_level, int risk)
{
    bool is_xau = is_gold(symbol);
    double free_margin = AccountFreeMargin();    

    double pips_delta = 0, risk_value = free_margin * risk / 100, ret = 0.01;

    int mult;
    if (is_xau) // 10 pips = 1$
    {
        pips_delta = NormalizeDouble(MathAbs(current_price - sl_level), 1);
        mult = risk_value / pips_delta;

        if (mult)
        {
            ret *= mult;
        }
    }
    else if (symbol == "BTCUSD") // 100pips = 1$
    {
        pips_delta = NormalizeDouble(MathAbs(current_price - sl_level), 0);
        double sl_value = pips_delta / 100;

        mult = MathFloor(risk_value / sl_value);

        Print("sl_value: ", sl_value, "mult: ", mult);

        if (mult)
        {
            ret = ret * mult;
        }
    }
    else // fx pairs
    {
        pips_delta = get_pips_delta(symbol, current_price, sl_level);
        
        ret = CalculateLotSize(
            free_margin,
            symbol,
            pips_delta,
            risk,
            MarketInfo(symbol, MODE_MINLOT),
            MarketInfo(symbol, MODE_MAXLOT),
            MarketInfo(symbol, MODE_LOTSTEP)
        );
    }

    Print("Risk value: ", risk_value, "Risk percents: ", risk);
    Print("current_price: ", current_price, "sl_level: ", sl_level, " pips_delta: ", pips_delta, " mult: ", mult);
    Print("calc_lots_size: ", ret);

    return ret;
}

// Функция для расчета дельты в пипсах
double get_pips_delta(string symbol, double p1, double p2)
{
   if (is_gold(symbol)) 
   {
      return MathAbs(p1 - p2) * 10;
   } 
   else
   {
      int digits = (int)MarketInfo(symbol, MODE_DIGITS);
      Print("digits: ", digits, "symbol: ", symbol);
      double nPoint = CalculateNormalizedDigits(digits);
      Print("nPoint: ", nPoint);
      return MathAbs(p1 - p2) / nPoint;
   }
}

// Функция для нормализации цифр
double CalculateNormalizedDigits(int digits)
{
   if(digits<=3){
      return(0.01);
   }
   else if(digits>=4){
      return(0.0001);
   }
   else return(0);
}

// Функция для модификации цены пары
double modify_pair_price(string pair, double price, int pips)
{
   if (is_gold(pair)) 
   {
      return price + pips / 10;
   }
   else 
   {
      int digits = (int)MarketInfo(pair, MODE_DIGITS);
      double nPoint = CalculateNormalizedDigits(digits);

      return NormalizeDouble(price + pips * nPoint, digits);
   }
}

// Функция для установки состояния тикета
datetime ticket_state_set(string ticket, string flag, double value)
{
   return GlobalVariableSet(ticket + "-" + flag, value);
}

// Функция для расчета размера лота (из CORE__HELPERS.mqh)
double CalculateLotSize(double free_margin, string symbol, double StopDistanceInPips, int risk, double MinLotSize, double MaxLotSize, double LotStep)
{          
    int HalfDigits,DoubleDigits;
    HalfDigits = MathFloor(MarketInfo(symbol, MODE_DIGITS) / 2);
    DoubleDigits = HalfDigits * 2;
    if(DoubleDigits != MarketInfo(symbol, MODE_DIGITS))  StopDistanceInPips = StopDistanceInPips * 10;

    double StopDistance = StopDistanceInPips * MarketInfo(symbol, MODE_POINT);

    double LotsByDistance = NormalizeDouble((free_margin * risk * MarketInfo(symbol, MODE_POINT)) / (100 * StopDistance * MarketInfo(symbol, MODE_TICKVALUE)), 2);
    if(LotsByDistance < MinLotSize)    { LotsByDistance = MinLotSize; }
    if(LotsByDistance > MaxLotSize)    { LotsByDistance = MaxLotSize; }
    LotsByDistance = NormalizeDouble(LotsByDistance / LotStep,0) * LotStep;
    
    double ActualRiskByDistance = (LotsByDistance * 100 * StopDistance * MarketInfo(symbol, MODE_TICKVALUE)) / (free_margin * MarketInfo(symbol, MODE_POINT));
    return DoubleToStr(LotsByDistance, 2);
}

int periods[] = {
   1,5,240
};


string get_symbol_name(string symbol)
{
   if (symbol == "BTCUSD")
   {
      return symbol;
   }

   return symbol + symbol_suffix;
}

double get_flag(string prefix, string flag="")
{
   return GlobalVariableGet(prefix + "-" + flag);
}

void del_flag(string prefix, string flag="")
{
   GlobalVariableDel(prefix + "-" + flag);
}

void SetLabel(string name,string txt,int corner, int x,int y,string font,int size,int angle,color clr, int window=0)
{
   if (ObjectFind(name) == -1)
   {
      ObjectCreate(name, OBJ_LABEL, window, 0, 0);
   }

   ObjectSetText(name, txt, size, font, clr);
   ObjectSet(name, OBJPROP_XDISTANCE, x);
   ObjectSet(name, OBJPROP_YDISTANCE, y);
   ObjectSet(name, OBJPROP_CORNER, corner);
   ObjectSet(name, OBJPROP_ANGLE, angle);
}

// Add these global variables at the top of the file with other globals
bool glbTestButtonCreated = false;
string testButtonName = "SEND_TEST_DATA_BTN";
bool glbOrderClosedTestButtonCreated = false;
string orderClosedTestButtonName = "SEND_ORDER_CLOSED_TEST_BTN";

// Add this function to create the button
void CreateTestButton()
{
   if(!glbTestButtonCreated)
   {
      ObjectCreate(testButtonName, OBJ_BUTTON, 0, 0, 0);
      ObjectSetText(testButtonName, "Send Test Data", 10, "Arial", White);
      ObjectSet(testButtonName, OBJPROP_XDISTANCE, 10);
      ObjectSet(testButtonName, OBJPROP_YDISTANCE, 40);
      ObjectSet(testButtonName, OBJPROP_XSIZE, 120);
      ObjectSet(testButtonName, OBJPROP_YSIZE, 30);
      ObjectSet(testButtonName, OBJPROP_BGCOLOR, Blue);
      ObjectSet(testButtonName, OBJPROP_BORDER_COLOR, White);
      glbTestButtonCreated = true;
   }
   
   if(!glbOrderClosedTestButtonCreated)
   {
      ObjectCreate(orderClosedTestButtonName, OBJ_BUTTON, 0, 0, 0);
      ObjectSetText(orderClosedTestButtonName, "Send Order Closed Test", 10, "Arial", White);
      ObjectSet(orderClosedTestButtonName, OBJPROP_XDISTANCE, 140);
      ObjectSet(orderClosedTestButtonName, OBJPROP_YDISTANCE, 40);
      ObjectSet(orderClosedTestButtonName, OBJPROP_XSIZE, 150);
      ObjectSet(orderClosedTestButtonName, OBJPROP_YSIZE, 30);
      ObjectSet(orderClosedTestButtonName, OBJPROP_BGCOLOR, Red);
      ObjectSet(orderClosedTestButtonName, OBJPROP_BORDER_COLOR, White);
      glbOrderClosedTestButtonCreated = true;
   }
}

// Add this function to send test data to all clients
void SendTestOrdersData()
{
   // Create the test JSON data
   CJAVal testData;
   testData["event"] = "orders_summary";

   // Create symbols data
   CJAVal symbolsData;

   // XAUUSD data
   CJAVal xauusdData;
   xauusdData["profit"] = 150.75;
   xauusdData["percent"] = 2.5;
   symbolsData["XAUUSD"].Set(xauusdData);

   // EURUSD data
   CJAVal eurusdData;
   eurusdData["profit"] = -50.25;
   eurusdData["percent"] = -0.8;
   symbolsData["EURUSD"].Set(eurusdData);

   // GBPJPY data
   CJAVal gbpjpyData;
   gbpjpyData["profit"] = 75.00;
   gbpjpyData["percent"] = 1.2;
   symbolsData["GBPJPY"].Set(gbpjpyData);

   // Add symbols data to main object
   testData["symbols"].Set(symbolsData);
   testData["total_balance"] = 10000.00;

   // Send to all connected clients
   for (int i = ArraySize(glbClients) - 1; i >= 0; i--)
   {
      ClientSocket* pClient = glbClients[i];

      // Check if client is still connected
      if (pClient != NULL && pClient.IsSocketConnected())
      {
         Print("SOCKET SERVER SENDING TEST DATA: ", testData.Serialize());
         pClient.Send(testData.Serialize() + "\r\n");
      }
      else
      {
         // Socket is no longer connected, remove it
         Print("Client ", i, " is no longer connected, removing from list");

         // Delete the client object
         delete pClient;

         // And remove from the array
         int ctClients = ArraySize(glbClients);
         for (int j = i + 1; j < ctClients; j++)
         {
            glbClients[j - 1] = glbClients[j];
         }
         ctClients--;
         ArrayResize(glbClients, ctClients);
      }
   }
}

// Add this function to send test order closed data to all clients
void SendTestOrderClosedData()
{
   // Create the test JSON data for order_closed event
   CJAVal testData;
   testData["event"] = "order_closed";
   
   // Create order data
   CJAVal orderData;
   orderData["id"] = "23434";
   orderData["profit"] = 125.50;
   orderData["symbol"] = "XAUUSD";
   orderData["percent"] = 1.25;
   orderData["lots"] = 0.1;
   orderData["profit_points"] = 125;
   orderData["open_time"] = (int)(TimeCurrent() - 3600); // 1 hour ago
   orderData["close_time"] = (int)TimeCurrent();
   orderData["duration_min"] = 60.0;
   
   testData["order"].Set(orderData);
   testData["total_balance"] = 10000.00;

   // Send to all connected clients
   for (int i = ArraySize(glbClients) - 1; i >= 0; i--)
   {
      ClientSocket* pClient = glbClients[i];

      // Check if client is still connected
      if (pClient != NULL && pClient.IsSocketConnected())
      {
         Print("SOCKET SERVER SENDING ORDER CLOSED TEST DATA: ", testData.Serialize());
         pClient.Send(testData.Serialize() + "\r\n");
      }
      else
      {
         // Socket is no longer connected, remove it
         Print("Client ", i, " is no longer connected, removing from list");

         // Delete the client object
         delete pClient;

         // And remove from the array
         int ctClients = ArraySize(glbClients);
         for (int j = i + 1; j < ctClients; j++)
         {
            glbClients[j - 1] = glbClients[j];
         }
         ctClients--;
         ArrayResize(glbClients, ctClients);
      }
   }
}

class LocalCommands {
   public:
   LocalCommands() {
      // db_filepath = data_folder + "\\" + "ripper_db.txt";
   }

   ~LocalCommands() {

   }

   void set_command_output(string folder, CJAVal &output)
   {

   }

   CJAVal get_input_commands(string folder)
   {
      CJAVal ret;

      return ret;
   }


   string create_jforex_picsfeed_request(string symbol, CJAVal* data = NULL)
   {
      string filename = data_folder + "\\jf_requests\\" + symbol;
      if (FileIsExist(filename))
      {
         FileDelete(filename);
      }
      int filehandle = FileOpen(filename, FILE_WRITE | FILE_TXT);
      if (data)
      {
         FileWrite(filehandle, data.Serialize());
      }
      FileClose(filehandle);
      return filename;
   }

   string picsfeed_request_folder()
   {
      return data_folder + "\\mt4_pic_requests\\";
   }

   string create_mt4_picsfeed_request(string symbol, CJAVal* data = NULL)
   {
      string filename = picsfeed_request_folder() + symbol;
      if (FileIsExist(filename))
      {
         FileDelete(filename);
      }

      int filehandle = FileOpen(filename, FILE_WRITE | FILE_TXT);
      if (data)
      {
         FileWrite(filehandle, data.Serialize());
      }
      FileClose(filehandle);

      return filename;
   }

   string jf_open_chart_filename(string pic_name)
   {
      return data_folder + "\\jf_show_chart_requests\\" + pic_name;
   }

   string create_jf_open_chart_request(string symbol)
   {
      string filename = jf_open_chart_filename(symbol);
      if (FileIsExist(filename))
      {
         FileDelete(filename);
      }
      int filehandle = FileOpen(filename, FILE_WRITE | FILE_TXT);
      FileClose(filehandle);
      return filename;
   }

   string get_picsfeed_request(string pic_name)
   {
      string file_name;
      long search_handle = FileFindFirst(picsfeed_request_folder() + "*", file_name);
      string ret = search_handle != INVALID_HANDLE ? file_name : "";
      FileFindClose(search_handle);
      return ret;
   }

   bool remove_picsfeed_request(string pic_name, string symbol)
   {
      bool ret = false;
      string filename = picsfeed_request_folder() + symbol;
      if (FileIsExist(filename))
      {
         FileDelete(filename);
         ret = true;
      }
      return ret;
   }

   string create_chart_command(string chart_place, string command, CJAVal*params)
   {
      params["command"] = command;


      Print("create_request_file -- ", params.Serialize());
      string filename = data_folder + "\\" + "chart_place_" + chart_place;
      int filehandle = FileOpen(filename, FILE_WRITE | FILE_TXT);
      FileWrite(filehandle, params.Serialize());
      FileClose(filehandle);

      return filename;
   }

   CJAVal proccess_commands(string chart_place)
   {
      CJAVal ret;

      string filename = data_folder + "\\" + "chart_place_" + chart_place;

      if (FileIsExist(filename))
      {
         int file_handle = FileOpen(filename, FILE_READ | FILE_TXT);
         int str_size = FileReadInteger(file_handle, INT_VALUE);
         string str = FileReadString(file_handle, str_size);
         FileClose(file_handle);

         //--- print the string
         PrintFormat(str);

         //Process command
         CJAVal command;
         command.Deserialize(str);

         Print("Found data folder command");
         PrintFormat(command.Serialize());

         if (command["command"].ToStr() == "move_chart_left")
         {
            Print("Move chart left");
            // build_chart_objects(Symbol(), Period());
            // move_chart_left(command);

            // ret["processed_cmd"] = command["command"].ToStr();
         }
         else if (command["command"].ToStr() == "change_period")
         {
            int period = command["period"].ToInt();

            if (Period() != period)
            {
               ChartSetSymbolPeriod(ChartID(), Symbol(), period);
            }
         }
         else if (command["command"].ToStr() == "change_symbol")
         {
            string symbol = command["symbol"].ToStr();

            if (Symbol() != symbol)
            {
               ChartSetSymbolPeriod(ChartID(), symbol, Period());
            }
         }
         else
         {
            ret.Set(command);
            Print("No such command handler: ", command["command"].ToStr());
         }

         FileDelete(filename);

      }

      return ret;
   }


   // void move_chart_left(CJAVal &command)
   // {
   //    long handle = ChartID();
   //    string comm = "";

   //    Print("Process move_chart_left");
   //    ChartSetInteger(handle, CHART_AUTOSCROLL, false);
   //    ChartSetInteger(handle, CHART_SHIFT, true);

   //    int period = Period();//command["period"].ToInt();
   //    int dt = command["datetime"].ToInt();
   //    double price = command["price"].ToDbl();
   //    int x, y;
   //    // if (period != Period())
   //    // {
   //    //    ChartSetSymbolPeriod(ChartID(), Symbol(), period);
   //    // }

   //    int shift = iBarShift(Symbol(), period, dt);
   //    //ChartNavigate(handle,CHART_END, -1);
   //    int shift_delta = period == 1 ? 150 : 0;
   //    ChartNavigate(handle, CHART_END, -(shift - WindowBarsPerChart() / 2 + shift_delta));

   //    // show vertical hor lines
   //    int color1 = command["direction"].ToStr() == "sell" ? Red : Lime;
   //    ui_set_vertical_line(dt, color1);
   //    ui_set_horisontal_line(price, period == 1 ? Red : Black);

   //    // set price label
   //    // ChartTimePriceToXY(0,0,dt,price,x,y);
   //    ObjectDelete(ChartID(), "PRICE_LABEL");

   //    //

   //    // SetPriceText("PRICE_LABEL", dt, price);
   //    // string price_format = "%.2f";
   //    // if (Symbol() == "XAUUSD" || Symbol() == "BTCUSD")
   //    // {
   //    //    price_format = "%.0f";
   //    //    price = " " + ((int)Bid);
   //    // }

   //    // SetLabel("PRICE_LABEL", StringFormat(price_format, price), CORNER_RIGHT_UPPER, 0, 70, "Georgia", 28, 0 ,Green);
   // }

};
//=============================================================

/* ###################################################################

Example socket server.
Code can be used as both MQ4 and MQ5 (on both 32-bit and 64-bit MT5)

Receives messages from the example client and simply writes them
to the Experts log.

Also contains functionality for handling files sent by the example 
file-sender script.

In addition, you can telnet into the server's port. Any CRLF-terminated
message you type is similarly printed to the Experts log. You
can also type in the commands "quote", to which the server reponds
with the current price of its chart, or "close", which causes the
server to shut down the connection.

As well as demonstrating server functionality, the use of Receive()
and the event-driven handling are also applicable to a client
which needs to receive data from the server as well as just sending it.

################################################################### */


#property strict

// --------------------------------------------------------------------
// Include socket library, asking for event handling
// --------------------------------------------------------------------

#define SOCKET_LIBRARY_USE_EVENTS
#include <socket-library-mt4-mt5.mqh>

#include "jAson.mqh";


// --------------------------------------------------------------------
// EA user inputs
// --------------------------------------------------------------------

input ushort   ServerPort = 23456;  // Server port
input bool     print_events = false;  // Print events to Experts log


// --------------------------------------------------------------------
// Global variables and constants
// --------------------------------------------------------------------

// Frequency for EventSetMillisecondTimer(). Doesn't need to 
// be very frequent, because it is just a back-up for the 
// event-driven handling in OnChartEvent()
#define TIMER_FREQUENCY_MS    1000

// Server socket
ServerSocket * glbServerSocket = NULL;

// Array of current clients
ClientSocket * glbClients[];

// Watch for need to create timer;
bool glbCreatedTimer = false;

LocalCommands* localCommands;

// --------------------------------------------------------------------
// Initialisation - set up server socket
// --------------------------------------------------------------------

void OnInit()
{

   localCommands = new LocalCommands();

   // If the EA is being reloaded, e.g. because of change of timeframe,
   // then we may already have done all the setup. See the 
   // termination code in OnDeinit.
   if (glbServerSocket)
   {
      Print("Reloading EA with existing server socket");
   }
   else
   {
      // Create the server socket
      glbServerSocket = new ServerSocket(ServerPort, false);
      if (glbServerSocket.Created())
      {
         Print("Server socket created");

         // Note: this can fail if MT4/5 starts up
         // with the EA already attached to a chart. Therefore,
         // we repeat in OnTick()
         glbCreatedTimer = EventSetMillisecondTimer(TIMER_FREQUENCY_MS);
      }
      else
      {
         Print("Server socket FAILED - is the port already in use?");
      }
   }

   SetLabel("SOCKET_PORT", "PORT: " + ServerPort + " | debug: " + (debug_mode ? "true" : "false"), CORNER_RIGHT_UPPER, 10, 10, "Georgia", 15, 0 ,Black);
   
   // Create test button
   CreateTestButton();
}

// --------------------------------------------------------------------
// Termination - free server socket and any clients
// --------------------------------------------------------------------

void OnDeinit(const int reason)
{
   delete localCommands;
   
   // Delete the test buttons
   ObjectDelete(testButtonName);
   glbTestButtonCreated = false;
   ObjectDelete(orderClosedTestButtonName);
   glbOrderClosedTestButtonCreated = false;

   switch (reason) {
      case REASON_CHARTCHANGE:
         // Keep the server socket and all its clients if 
         // the EA is going to be reloaded because of a 
         // change to chart symbol or timeframe 
         break;

      default:
         // For any other unload of the EA, delete the 
         // server socket and all the clients 
         glbCreatedTimer = false;

         // Delete all clients currently connected
         for (int i = 0; i < ArraySize(glbClients); i++) {
            delete glbClients[i];
         }
         ArrayResize(glbClients, 0);

         // Free the server socket. *VERY* important, or else
         // the port number remains in use and un-reusable until
         // MT4/5 is shut down
         delete glbServerSocket;
         glbServerSocket = NULL;
         Print("Server socket terminated");
         break;
   }
}

// --------------------------------------------------------------------
// Timer - accept new connections, and handle incoming data from clients.
// Secondary to the event-driven handling via OnChartEvent(). Most
// socket events should be picked up faster through OnChartEvent()
// rather than being first detected in OnTimer()
// --------------------------------------------------------------------

void OnTimer()
{
   // Accept any new pending connections
   AcceptNewConnections();

   // Process any incoming data on each client socket,
   // bearing in mind that HandleSocketIncomingData()
   // can delete sockets and reduce the size of the array
   // if a socket has been closed

   for (int i = ArraySize(glbClients) - 1; i >= 0; i--)
   {
      HandleSocketIncomingData(i);
   }

   // Check created tline event from 
   // HandleTlineCreatedEvent();

   HandleOpenedOrdersEvent();
   HandleReloadDbEvent();

   // Проверка и отправка закрытых ордеров
   CheckAndSendClosedOrderEvent();
}

void CheckAndSendClosedOrderEvent()
{
   // Проверяем до 5 переменных new_closed_order0 ... new_closed_order4
   for (int idx = 0; idx < 5; idx++)
   {
      string gvName = idx == 0 ? "new_closed_order" : "new_closed_order" + IntegerToString(idx);
      if (GlobalVariableCheck(gvName))
      {
         double ticket = GlobalVariableGet(gvName);
         GlobalVariableDel(gvName);

         if (OrderSelect(ticket, SELECT_BY_TICKET))
         {
            string symbol = OrderSymbol();
            double profit = OrderProfit() + OrderCommission() + OrderSwap();
            double lots = OrderLots();
            string order_id = OrderComment();

            // Считаем пункты
            double openPrice = OrderOpenPrice();
            double closePrice = OrderClosePrice();
            int profitPoints = 0;

            if (is_gold(symbol))
            {
               profitPoints = (int)(MathAbs(closePrice - openPrice) * 10);
               if (OrderType() == OP_BUY)
                  profitPoints = (int)((closePrice - openPrice) * 10);
               else if (OrderType() == OP_SELL)
                  profitPoints = (int)((openPrice - closePrice) * 10);
            }
            else
            {
               int digits = (int)MarketInfo(symbol, MODE_DIGITS);
               double nPoint = CalculateNormalizedDigits(digits);
               if (OrderType() == OP_BUY)
                  profitPoints = (int)((closePrice - openPrice) / nPoint);
               else if (OrderType() == OP_SELL)
                  profitPoints = (int)((openPrice - closePrice) / nPoint);
            }

            double percent = AccountBalance() > 0 ? (profit / AccountBalance()) * 100 : 0;
            datetime openTime = OrderOpenTime();
            datetime closeTime = OrderCloseTime();
            int secondsOpen = (int)(closeTime - openTime);

            // Формируем JSON
            CJAVal eventJson;
            eventJson["event"] = "order_closed";
            CJAVal symbolsJson;
            CJAVal symbolData;
            symbolData["ticket"] = ticket;
            symbolData["id"] = order_id;
            symbolData["profit"] = profit;
            symbolData["symbol"] = symbol;
            symbolData["percent"] = NormalizeDouble(percent, 2);
            symbolData["lots"] = lots;
            symbolData["profit_points"] = profitPoints;
            symbolData["open_time"] = (int)openTime;
            symbolData["close_time"] = (int)closeTime;
            symbolData["duration_min"] = NormalizeDouble(secondsOpen / 60, 2); ;
            
            eventJson["order"].Set(symbolData);
            eventJson["total_balance"] = AccountBalance();

            string msg = eventJson.Serialize();

            // Отправляем всем клиентам
            for (int k = ArraySize(glbClients) - 1; k >= 0; k--)
            {
               ClientSocket* pClient = glbClients[k];
               if (pClient != NULL && pClient.IsSocketConnected())
               {
                  Print("SOCKET SERVER ORDER CLOSED EVENT: ", msg);
                  pClient.Send(msg + "\r\n");
               }
            }
         }
      }
   }
}

void HandleReloadDbEvent()
{
   if (get_flag("socket_server", "reload_db"))
   {
      // Send watchlist stats
      SendWatchlistStatsEvent();

      del_flag("socket_server", "reload_db");
   }
}

// Helper function to send orders_summary event to all clients
void send_orders_summary_event(CJAVal &ordersData)
{
   for (int i = ArraySize(glbClients) - 1; i >= 0; i--)
   {
      ClientSocket* pClient = glbClients[i];
      if (pClient != NULL && pClient.IsSocketConnected())
      {
         if (print_events) Print("SOCKET SERVER MT4 ORDERS EVENT: ", ordersData.Serialize());
         pClient.Send(ordersData.Serialize() + "\r\n");
      }
      else
      {
         Print("Client ", i, " is no longer connected, removing from list");
         delete pClient;
         for (int j = i; j < ArraySize(glbClients) - 1; j++)
         {
            glbClients[j] = glbClients[j + 1];
         }
         ArrayResize(glbClients, ArraySize(glbClients) - 1);
      }
   }
}

void HandleOpenedOrdersEvent()
{
   static bool was_orders_sent = false;
   if (OrdersTotal() > 0)
   {
      CJAVal ordersData;
      ordersData["event"] = "orders_summary";
      CJAVal symbolsData;

      // Arrays to store unique symbols and their total profits and lots
      string symbols[];
      double profits[];
      double totalLots[];
      int profitPoints[]; // Renamed from maxProfitPoints to profitPoints
      int symbolCount = 0;

      // First pass: collect all unique symbols
      if (OrdersTotal() > 0)
      {
         for (int i = 0; i < OrdersTotal(); i++)
         {
            if (OrderSelect(i, SELECT_BY_POS, MODE_TRADES))
            {
               string symbol = OrderSymbol();
               bool found = false;

               // Check if symbol already in our list
               for (int j = 0; j < symbolCount; j++)
               {
                  if (symbols[j] == symbol)
                  {
                     found = true;
                     break;
                  }
               }

               // Add new symbol to the list
               if (!found)
               {
                  ArrayResize(symbols, symbolCount + 1);
                  ArrayResize(profits, symbolCount + 1);
                  ArrayResize(totalLots, symbolCount + 1);
                  ArrayResize(profitPoints, symbolCount + 1);
                  symbols[symbolCount] = symbol;
                  profits[symbolCount] = 0;
                  totalLots[symbolCount] = 0;
                  profitPoints[symbolCount] = 0;
                  symbolCount++;
               }
            }
         }
      }

      // Second pass: calculate total profit and lots for each symbol
      if (OrdersTotal() > 0)
      {
         for (int i = 0; i < OrdersTotal(); i++)
         {
            if (OrderSelect(i, SELECT_BY_POS, MODE_TRADES))
            {
               string symbol = OrderSymbol();
               double profit = OrderProfit() + OrderCommission() + OrderSwap();
               double lots = OrderLots();

               // Calculate profit in points
               int currentProfitPoints = 0;
               double openPrice = OrderOpenPrice();
               double currentPrice = OrderType() == OP_BUY ? MarketInfo(symbol, MODE_BID) : MarketInfo(symbol, MODE_ASK);
               double point = MarketInfo(symbol, MODE_POINT);

               if (OrderType() == OP_BUY)
                  currentProfitPoints = (int)((currentPrice - openPrice) / point);
               else if (OrderType() == OP_SELL)
                  currentProfitPoints = (int)((openPrice - currentPrice) / point);

               // Find symbol in our list and add profit and lots
               for (int j = 0; j < symbolCount; j++)
               {
                  if (symbols[j] == symbol)
                  {
                     profits[j] += profit;
                     totalLots[j] += lots;

                     // Sum up profit points for all orders of this symbol
                     // This will give us the net profit points across all orders
                     profitPoints[j] += currentProfitPoints;

                     break;
                  }
               }
            }
         }
      }

      double accountBalance = AccountBalance();

      // Create JSON objects for each symbol with profit percentage and total lots
      for (int i = 0; i < symbolCount; i++)
      {
         CJAVal symbolData;
         double profitPercent = (profits[i] / accountBalance) * 100;

         symbolData["profit"] = profits[i];
         symbolData["percent"] = NormalizeDouble(profitPercent, 2);
         symbolData["lots"] = NormalizeDouble(totalLots[i], 2);
         symbolData["profit_points"] = profitPoints[i]; // Renamed from max_profit_points to profit_points

         // Add to symbols data
         symbolsData[symbols[i]].Set(symbolData);
      }

      // Add symbols data to main object
      ordersData["symbols"].Set(symbolsData);
      ordersData["total_balance"] = accountBalance;

      send_orders_summary_event(ordersData);
      was_orders_sent = true;
   }
   else if (was_orders_sent)
   {
      // Orders just became zero, send empty orders_summary event
      CJAVal ordersData;
      ordersData["event"] = "orders_summary";
      CJAVal symbolsData;
      ordersData["symbols"].Set(symbolsData); // empty
      ordersData["total_balance"] = AccountBalance();
      send_orders_summary_event(ordersData);
      was_orders_sent = false;
   }
}

void SendWatchlistStatsEvent()
{
   CJAVal watchlistStats = analyze_watchlist_by_symbol();

   CJAVal data2;
   data2["event"] = "watchlist_stats";
   data2["data"].Set(watchlistStats);
   Print("SOCKET SERVER MT4 EVENT", data2.Serialize());

   for (int i = ArraySize(glbClients) - 1; i >= 0; i--)
   {
      // send watchlist stats
      ClientSocket* pClient = glbClients[i];
      pClient.Send(data2.Serialize());
   }
}

void HandleTlineCreatedEvent()
{
   if (get_flag("socket_server", "tline_created"))
   {
      del_flag("socket_server", "tline_created");
      
      CJAVal data;
      data["event"] = "tline_created";
      Print("SOCKET SERVER MT4 EVENT", data.Serialize());
      for (int i = ArraySize(glbClients) - 1; i >= 0; i--)
      {
         ClientSocket* pClient = glbClients[i];
         pClient.Send(data.Serialize());
      }

      // Send watchlist stats
      SendWatchlistStatsEvent();
   }
}

// --------------------------------------------------------------------
// Sends comprehensive welcome message with commands list and version
// --------------------------------------------------------------------

void SendWelcomeMessage(ClientSocket* pClient)
{
   CJAVal welcomeMsg;
   welcomeMsg["event"] = "welcome";
   welcomeMsg["version"] = "1.4.0";
   welcomeMsg["server_name"] = "RIPPER_MT4_SOCKET_SERVER";
   welcomeMsg["port"] = IntegerToString(ServerPort);
   welcomeMsg["debug_mode"] = debug_mode;
   welcomeMsg["symbol_suffix"] = symbol_suffix;
   
   // Account information
   CJAVal accountInfo;
   accountInfo["balance"] = AccountBalance();
   accountInfo["equity"] = AccountEquity();
   accountInfo["margin"] = AccountMargin();
   accountInfo["free_margin"] = AccountFreeMargin();
   accountInfo["currency"] = AccountCurrency();
   accountInfo["company"] = AccountCompany();
   accountInfo["server"] = AccountServer();
   welcomeMsg["account"].Set(accountInfo);
   
   string welcomeJson = welcomeMsg.Serialize();
   pClient.Send(welcomeJson + "\r\n");
   Print("Sent welcome message to new client: ", welcomeJson);
}

// --------------------------------------------------------------------
// Accepts new connections on the server socket, creating new
// entries in the glbClients[] array
// --------------------------------------------------------------------

void AcceptNewConnections()
{
   // Keep accepting any pending connections until Accept() returns NULL
   ClientSocket * pNewClient = NULL;
   do {
      pNewClient = glbServerSocket.Accept();
      if (pNewClient != NULL)
      {
         int sz = ArraySize(glbClients);
         ArrayResize(glbClients, sz + 1);
         glbClients[sz] = pNewClient;
         Print("New client connection");

         // Send comprehensive welcome message with commands and version
         SendWelcomeMessage(pNewClient);

         // send watchlist stats
         SendWatchlistStatsEvent();
      }
      
   } while (pNewClient != NULL);
}


// --------------------------------------------------------------------
// Handles any new incoming data on a client socket, identified
// by its index within the glbClients[] array. This function
// deletes the ClientSocket object, and restructures the array,
// if the socket has been closed by the client
// --------------------------------------------------------------------

void HandleSocketIncomingData(int idxClient)
{
   ClientSocket * pClient = glbClients[idxClient];

   // Keep reading CRLF-terminated lines of input from the client
   // until we run out of new data
   bool bForceClose = false; // Client has sent a "close" message
   string strCommand;
   CJAVal input_json;

   do
   {
      strCommand = pClient.Receive("\r\n");
      //Comment(strCommand);

      if (StringLen(strCommand) != 0)
      {
         input_json.Deserialize(strCommand);
         Print("Received: ", input_json.Serialize());
         string output_json = process_socket_command(input_json).Serialize();
         pClient.Send(output_json);
         Print("Answered: ", output_json);
      }

   } while (strCommand != "");

   // If the socket has been closed, or the client has sent a close message,
   // release the socket and shuffle the glbClients[] array
   if (!pClient.IsSocketConnected() || bForceClose) {
      Print("Client has disconnected");

      // Client is dead. Destroy the object
      delete pClient;
      
      // And remove from the array
      int ctClients = ArraySize(glbClients);
      for (int i = idxClient + 1; i < ctClients; i++) {
         glbClients[i - 1] = glbClients[i];
      }
      ctClients--;
      ArrayResize(glbClients, ctClients);
   }
}

// Command handler functions
CJAVal handle_get_open_orders(CJAVal &input_json)
{
   CJAVal output_json;
   output_json["orders"] = "orders info";
   return output_json;
}

CJAVal handle_show_symbol(CJAVal &input_json)
{
   CJAVal output_json;
   CJAVal params;
   
   params["symbol"] = get_symbol_name(input_json["symbol"].ToStr());
   localCommands.create_chart_command("right", "change_symbol", &params);
   localCommands.create_chart_command("right2", "change_symbol", &params);
   localCommands.create_chart_command("right3", "change_symbol", &params);
   localCommands.create_chart_command("left", "change_symbol", &params);
   localCommands.create_chart_command("details", "change_symbol", &params);
   
   return output_json;
}

CJAVal handle_close_positions(CJAVal &input_json)
{
   CJAVal output_json;
   string symbol = input_json["symbol"].ToStr();
   //symbol = get_symbol_name(symbol);

   // Close all positions for the specified symbol
   int ids[];
   int count = 0;

   if (OrdersTotal() > 0)
   {
      // First pass: collect all order tickets for the symbol
      for (int order_counter = 0; order_counter < OrdersTotal(); order_counter++)
      {
         if (OrderSelect(order_counter, SELECT_BY_POS, MODE_TRADES) == true)
         {
            if (OrderSymbol() == symbol)
            {
               ArrayResize(ids, count + 1);
               ids[count] = OrderTicket();
               count++;
            }
         }
      }

      // Second pass: close all collected orders
      bool all_closed = true;
      for (int i = 0; i < count; i++)
      {
         if (OrderSelect(ids[i], SELECT_BY_TICKET) == true)
         {
            bool result = false;
            if (OrderType() <= OP_SELL) // Market orders
            {
               double close_price = OrderType() == OP_BUY ? MarketInfo(symbol, MODE_BID) : MarketInfo(symbol, MODE_ASK);
               result = OrderClose(ids[i], OrderLots(), close_price, 300, Violet);
            }
            else // Pending orders
            {
            //    result = OrderDelete(ids[i], Violet);
            }

            if (!result)
            {
               Print("Error closing order #", ids[i], ": ", GetLastError());
               all_closed = false;
            }
         }
      }

      // Add result to output
      output_json["closed_count"] = count;
      output_json["all_closed"] = all_closed;
   }
   else
   {
      output_json["closed_count"] = 0;
      output_json["all_closed"] = true;
   }

   // Send updated orders summary to all clients after closing positions
   HandleOpenedOrdersEvent();
   
   return output_json;
}

// Delete only pending BUY orders (OP_BUYLIMIT, OP_BUYSTOP) for a symbol (or current chart symbol if not provided)
CJAVal handle_delete_pending_buys(CJAVal &input_json)
{
   CJAVal output_json;
   string symbol = Symbol();
   if (input_json.FindKey("symbol"))
   {
      symbol = input_json["symbol"].ToStr();
   }

   int ids[];
   int count = 0;

   if (OrdersTotal() > 0)
   {
      // Collect pending BUY order tickets for the symbol
      for (int order_counter = 0; order_counter < OrdersTotal(); order_counter++)
      {
         if (OrderSelect(order_counter, SELECT_BY_POS, MODE_TRADES) == true)
         {
            int type = OrderType();
            //OrderSymbol() == symbol &&
            if ((type == OP_BUYLIMIT || type == OP_BUYSTOP))
            {
               ArrayResize(ids, count + 1);
               ids[count] = OrderTicket();
               count++;
            }
         }
      }

      bool all_deleted = true;
      for (int i = 0; i < count; i++)
      {
         if (OrderSelect(ids[i], SELECT_BY_TICKET) == true)
         {
            bool result = OrderDelete(ids[i], Violet);
            if (!result)
            {
               Print("Error deleting pending BUY order #", ids[i], ": ", GetLastError());
               all_deleted = false;
            }
         }
      }

      output_json["deleted_count"] = count;
      output_json["all_deleted"] = all_deleted;
   }
   else
   {
      output_json["deleted_count"] = 0;
      output_json["all_deleted"] = true;
   }

   HandleOpenedOrdersEvent();
   return output_json;
}

// Delete only pending SELL orders (OP_SELLLIMIT, OP_SELLSTOP) for a symbol (or current chart symbol if not provided)
CJAVal handle_delete_pending_sells(CJAVal &input_json)
{
   CJAVal output_json;
   string symbol = Symbol();
   if (input_json.FindKey("symbol"))
   {
      symbol = input_json["symbol"].ToStr();
   }

   int ids[];
   int count = 0;

   if (OrdersTotal() > 0)
   {
      // Collect pending SELL order tickets for the symbol
      for (int order_counter = 0; order_counter < OrdersTotal(); order_counter++)
      {
         if (OrderSelect(order_counter, SELECT_BY_POS, MODE_TRADES) == true)
         {
            int type = OrderType();
            // OrderSymbol() == symbol &&
            if ((type == OP_SELLLIMIT || type == OP_SELLSTOP))
            {
               ArrayResize(ids, count + 1);
               ids[count] = OrderTicket();
               count++;
            }
         }
      }

      bool all_deleted = true;
      for (int i = 0; i < count; i++)
      {
         if (OrderSelect(ids[i], SELECT_BY_TICKET) == true)
         {
            bool result = OrderDelete(ids[i], Violet);
            if (!result)
            {
               Print("Error deleting pending SELL order #", ids[i], ": ", GetLastError());
               all_deleted = false;
            }
         }
      }

      output_json["deleted_count"] = count;
      output_json["all_deleted"] = all_deleted;
   }
   else
   {
      output_json["deleted_count"] = 0;
      output_json["all_deleted"] = true;
   }

   HandleOpenedOrdersEvent();
   return output_json;
}

CJAVal handle_buy_order(CJAVal &input_json)
{
   CJAVal output_json;
   string symbol = input_json["symbol"].ToStr();
   int risk = input_json["risk"].ToInt();
   symbol = get_symbol_name(symbol);
   
   // Получаем текущую цену
   double current_price = MarketInfo(symbol, MODE_ASK);
   
   // Получаем размер stop loss в пипсах (по умолчанию 20)
   int sl_pips = 20;
   if (input_json.FindKey("sl_pips")) {
      sl_pips = input_json["sl_pips"].ToInt();
   }
   
   // Рассчитываем уровень stop loss
   double sl_level = modify_pair_price(symbol, current_price, -sl_pips); // Отрицательное значение для BUY (ниже цены)
   
   // Рассчитываем размер лота
   double lot_size = calc_lots_size(symbol, current_price, sl_level, risk);
   
   int slippage = 300;

   // --- Add uid to comment if present ---
   string order_comment = "";
   if (input_json.FindKey("id")) {
      order_comment = input_json["id"].ToStr();
   }
   
   Print("Opening BUY order - Symbol: ", symbol, " Price: ", current_price, " SL: ", sl_level, " SL pips: ", sl_pips, " Lots: ", lot_size, " Risk: ", risk);
   
   int ticket = -1;
   
   if (!debug_mode) {
      // Normalize prices for XAUUSD (2 decimal places) or other symbols
      double normalized_price = current_price;
      double normalized_sl = sl_level;
      if (is_gold(symbol)) {
         normalized_price = NormalizeDouble(current_price, 2);
         normalized_sl = NormalizeDouble(sl_level, 2);
      } else {
         normalized_price = NormalizeDouble(current_price, (int)MarketInfo(symbol, MODE_DIGITS));
         normalized_sl = NormalizeDouble(sl_level, (int)MarketInfo(symbol, MODE_DIGITS));
      }
      
      // Отправляем реальный ордер
      ticket = OrderSend(symbol, OP_BUY, lot_size, normalized_price, slippage, normalized_sl, 0, order_comment, 0, 0, Blue);
   } else {
      // Режим отладки - только логирование
      Print("DEBUG MODE: BUY order would be opened - Symbol: ", symbol, " Price: ", current_price, " SL: ", sl_level, " Lots: ", lot_size);
      ticket = 99999; // Виртуальный тикет для отладки
   }
   
   if(ticket > 0)
   {
      output_json["success"] = true;
      output_json["ticket"] = ticket;
      output_json["symbol"] = symbol;
      output_json["direction"] = "BUY";
      output_json["lot_size"] = lot_size;
      output_json["price"] = current_price;
      output_json["sl_level"] = sl_level;
      output_json["sl_pips"] = sl_pips;
      output_json["risk"] = risk;
      output_json["debug_mode"] = debug_mode;
      
      if (!debug_mode) {
         // Устанавливаем состояние тикета только для реальных ордеров
         ticket_state_set(ticket, "sl", sl_level);
         Print("BUY order opened successfully. Ticket: ", ticket, " SL: ", sl_level);
      } else {
         Print("DEBUG MODE: BUY order simulated successfully. Virtual Ticket: ", ticket);
      }
   }
   else
   {
      output_json["success"] = false;
      output_json["error"] = "Failed to open BUY order";
      output_json["symbol"] = symbol;
      output_json["error_code"] = GetLastError();
      output_json["debug_mode"] = debug_mode;
      Print("Failed to open BUY order. Error: ", GetLastError());
   }
   
   return output_json;
}

CJAVal handle_sell_order(CJAVal &input_json)
{
   CJAVal output_json;
   string symbol = input_json["symbol"].ToStr();
   int risk = input_json["risk"].ToInt();
   symbol = get_symbol_name(symbol);
   
   // Получаем текущую цену
   double current_price = MarketInfo(symbol, MODE_BID);
   
   // Получаем размер stop loss в пипсах (по умолчанию 20)
   int sl_pips = 20;
   if (input_json.FindKey("sl_pips")) {
      sl_pips = input_json["sl_pips"].ToInt();
   }
   
   // Рассчитываем уровень stop loss
   double sl_level = modify_pair_price(symbol, current_price, sl_pips); // Положительное значение для SELL (выше цены)
   
   // Рассчитываем размер лота
   double lot_size = calc_lots_size(symbol, current_price, sl_level, risk);
   
   int slippage = 3;

   // --- Add uid to comment if present ---
   string order_comment = "";
   if (input_json.FindKey("id")) {
      order_comment = input_json["id"].ToStr();
   }
   
   Print("Opening SELL order - Symbol: ", symbol, " Price: ", current_price, " SL: ", sl_level, " SL pips: ", sl_pips, " Lots: ", lot_size, " Risk: ", risk);
   
   int ticket = -1;
   
   if (!debug_mode) {
      // Normalize prices for XAUUSD (2 decimal places) or other symbols
      double normalized_price = current_price;
      double normalized_sl = sl_level;
      if (is_gold(symbol)) {
         normalized_price = NormalizeDouble(current_price, 2);
         normalized_sl = NormalizeDouble(sl_level, 2);
      } else {
         normalized_price = NormalizeDouble(current_price, (int)MarketInfo(symbol, MODE_DIGITS));
         normalized_sl = NormalizeDouble(sl_level, (int)MarketInfo(symbol, MODE_DIGITS));
      }
      
      // Отправляем реальный ордер
      ticket = OrderSend(symbol, OP_SELL, lot_size, normalized_price, slippage, normalized_sl, 0, order_comment, 0, 0, Red);
   } else {
      // Режим отладки - только логирование
      Print("DEBUG MODE: SELL order would be opened - Symbol: ", symbol, " Price: ", current_price, " SL: ", sl_level, " Lots: ", lot_size);
      ticket = 99999; // Виртуальный тикет для отладки
   }
   
   if(ticket > 0)
   {
      output_json["success"] = true;
      output_json["ticket"] = ticket;
      output_json["symbol"] = symbol;
      output_json["direction"] = "SELL";
      output_json["lot_size"] = lot_size;
      output_json["price"] = current_price;
      output_json["sl_level"] = sl_level;
      output_json["sl_pips"] = sl_pips;
      output_json["risk"] = risk;
      output_json["debug_mode"] = debug_mode;
      
      if (!debug_mode) {
         // Устанавливаем состояние тикета только для реальных ордеров
         ticket_state_set(ticket, "sl", sl_level);
         Print("SELL order opened successfully. Ticket: ", ticket, " SL: ", sl_level);
      } else {
         Print("DEBUG MODE: SELL order simulated successfully. Virtual Ticket: ", ticket);
      }
   }
   else
   {
      output_json["success"] = false;
      output_json["error"] = "Failed to open SELL order";
      output_json["symbol"] = symbol;
      output_json["error_code"] = GetLastError();
      output_json["debug_mode"] = debug_mode;
      Print("Failed to open SELL order. Error: ", GetLastError());
   }
   
   return output_json;
}

CJAVal handle_new_level_added(CJAVal &input_json)
{
   CJAVal output_json;
   string base_symbol = input_json["symbol"].ToStr();
   string symbol = get_symbol_name(base_symbol);
   string direction = input_json["direction"].ToStr(); // "BUY" or "SELL"
   double level_price = input_json["price"].ToDbl();
   int risk = input_json.FindKey("risk") ? input_json["risk"].ToInt() : 5; // default 5%
   int sl_pips = input_json.FindKey("sl_pips") ? input_json["sl_pips"].ToInt() : 20; // default 20 (for XAU 20 points)

   // Optional id/comment
   string order_comment = input_json.FindKey("id") ? input_json["id"].ToStr() : "";

   // Current prices
   double bid = MarketInfo(symbol, MODE_BID);
   double ask = MarketInfo(symbol, MODE_ASK);
   double point = MarketInfo(symbol, MODE_POINT);
   int digits = (int)MarketInfo(symbol, MODE_DIGITS);

   bool is_buy = (direction == "BUY");

   // Determine current price for lot calc and market trigger check
   double current_price = is_buy ? ask : bid;

   // Compute SL level only for lot sizing; market order will be without SL per spec
   double sl_level = is_buy ? modify_pair_price(symbol, current_price, -sl_pips)
                            : modify_pair_price(symbol, current_price,  sl_pips);

   double lot_size = calc_lots_size(symbol, current_price, sl_level, risk);

   int slippage = 300;

   // --- Logging inputs and computed values ---
   Print("[new_level_added] base_symbol:", base_symbol,
         " symbol:", symbol,
         " direction:", direction,
         " level_price:", DoubleToString(level_price, digits),
         " risk:", risk,
         " sl_pips:", sl_pips,
         " id:", order_comment);
   Print("[new_level_added] bid:", DoubleToString(bid, digits),
         " ask:", DoubleToString(ask, digits),
         " point:", point,
         " digits:", digits);
   Print("[new_level_added] is_buy:", is_buy,
         " current_price:", DoubleToString(current_price, digits),
         " sl_level:", DoubleToString(sl_level, digits),
         " lot_size:", lot_size,
         " slippage:", slippage);

   // Decide action: market vs pending
   bool place_market = false;
   if (is_buy)
   {
      // If current price above the provided level -> market BUY
      place_market = (current_price > level_price);
   }
   else
   {
      // SELL: if current price below the provided level -> market SELL
      place_market = (current_price < level_price);
   }

   Print("[new_level_added] place_market:", place_market);

   int ticket = -1;

   if (place_market)
   {
      if (!debug_mode)
      {
         // Normalize current price for XAUUSD (2 decimal places) or other symbols
         double normalized_price = current_price;
         if (is_gold(symbol)) {
            normalized_price = NormalizeDouble(current_price, 2);
         } else {
            normalized_price = NormalizeDouble(current_price, digits);
         }
         
         if (is_buy)
            ticket = OrderSend(symbol, OP_BUY, lot_size, normalized_price, slippage, 0, 0, order_comment, 0, 0, Blue);
         else
            ticket = OrderSend(symbol, OP_SELL, lot_size, normalized_price, slippage, 0, 0, order_comment, 0, 0, Red);
      }
      else
      {
         Print("DEBUG MODE: MARKET ", direction, " order would be opened - Symbol: ", symbol, " Price: ", current_price, " Lots: ", lot_size);
         ticket = 99999;
      }

      if (ticket > 0)
      {
         Print("[new_level_added] MARKET success ticket:", ticket,
               " dir:", direction,
               " symbol:", symbol,
               " price:", DoubleToString(current_price, digits),
               " lots:", lot_size,
               " risk:", risk);
         output_json["success"] = true;
         output_json["ticket"] = ticket;
         output_json["symbol"] = symbol;
         output_json["direction"] = direction;
         output_json["order_type"] = "market";
         output_json["lot_size"] = lot_size;
         output_json["price"] = current_price;
         output_json["risk"] = risk;
      }
      else
      {
         Print("[new_level_added] MARKET failed, err:", GetLastError());
         output_json["success"] = false;
         output_json["error"] = "Failed to open market order";
         output_json["symbol"] = symbol;
         output_json["error_code"] = GetLastError();
      }
   }
   else
   {
      // Place pending order at level. For BUY pending, add spread so candle close reaches level.
      double spread = (MarketInfo(symbol, MODE_ASK) - MarketInfo(symbol, MODE_BID));
      double pending_price = level_price;
      int pending_type = is_buy ? OP_BUYSTOP : OP_SELLSTOP;

      if (is_buy)
      {
         // add spread for BUY pending
         pending_price = level_price + spread;
      }
      
      // Normalize price for XAUUSD (2 decimal places) or other symbols
      if (is_gold(symbol)) {
         pending_price = NormalizeDouble(pending_price, 2);
      } else {
         pending_price = NormalizeDouble(pending_price, digits);
      }

      Print("[new_level_added] PENDING setup spread:", spread,
            " pending_type:", (is_buy ? "BUYSTOP" : "SELLSTOP"),
            " pending_price:", DoubleToString(pending_price, digits),
            " requested_level:", DoubleToString(level_price, digits));

      if (!debug_mode)
      {
         ticket = OrderSend(symbol, pending_type, lot_size, pending_price, slippage, 0, 0, order_comment, 0, 0, is_buy ? Blue : Red);
      }
      else
      {
         Print("DEBUG MODE: PENDING ", (is_buy ? "BUYSTOP" : "SELLSTOP"), " would be placed - Symbol: ", symbol, " Price: ", pending_price, " Lots: ", lot_size);
         ticket = 99998;
      }

      if (ticket > 0)
      {
         Print("[new_level_added] PENDING success ticket:", ticket,
               " type:", (is_buy ? "BUYSTOP" : "SELLSTOP"),
               " symbol:", symbol,
               " price:", DoubleToString(pending_price, digits),
               " lots:", lot_size,
               " risk:", risk);
         output_json["success"] = true;
         output_json["ticket"] = ticket;
         output_json["symbol"] = symbol;
         output_json["direction"] = direction;
         output_json["order_type"] = "pending";
         output_json["pending_type"] = (is_buy ? "BUYSTOP" : "SELLSTOP");
         output_json["lot_size"] = lot_size;
         output_json["price"] = pending_price;
         output_json["requested_level"] = level_price;
         output_json["risk"] = risk;
      }
      else
      {
         Print("[new_level_added] PENDING failed, err:", GetLastError());
         output_json["success"] = false;
         output_json["error"] = "Failed to place pending order";
         output_json["symbol"] = symbol;
         output_json["error_code"] = GetLastError();
      }
   }

   return output_json;
}

CJAVal handle_get_symbols_history(CJAVal &input_json)
{
   CJAVal output_json;
   
   // Get parameters from input
   string symbols_str = input_json["symbols"].ToStr();
   int history = input_json["history"].ToInt();
   
   // Default values if not provided
   if (history <= 0) history = 100;
   if (StringLen(symbols_str) == 0) symbols_str = Symbol();
   
   // Parse symbols array
   string symbols[];
   StringSplit(symbols_str, ',', symbols);
   
   // Use global periods array
   CJAVal ret;
   
   // Process each symbol
   for (int j = 0; j < ArraySize(symbols); j++)
   {
      string symbol = symbols[j];
      // Map symbol through get_symbol_name function to apply suffix
      string mapped_symbol = get_symbol_name(symbol);
      
      // Process each period using global periods array
      for (int i = 0; i < ArraySize(periods); i++)
      {
         int period = periods[i];
         string postData = "";
         
         // Get historical data using mapped symbol
         for (int k = history; k >= 0; k--)
         {
            double close = iClose(mapped_symbol, period, k);
            double open = iOpen(mapped_symbol, period, k);
            double high = iHigh(mapped_symbol, period, k);
            double low = iLow(mapped_symbol, period, k);
            datetime time = iTime(mapped_symbol, period, k);
            
            // Format: timestamp,open,high,low,close
            string candleData = StringConcatenate(time, ",", open, ",", high, ",", low, ",", close);
            postData = StringConcatenate(postData, candleData, ";");
         }
         
         // Add to result using original symbol name as key
         CJAVal temp;
         temp[(string)period] = postData;
         ret[symbol].Add(temp);
      }
   }
   
   output_json["data"].Set(ret);
   output_json["history"] = history;
   
   return output_json;
}

CJAVal handle_help_command(CJAVal &input_json)
{
   CJAVal output_json;
   // List of commands, descriptions, and examples
   CJAVal commands;

   // get_open_orders
   CJAVal get_open_orders;
   get_open_orders["description"] = "Get current open orders";
   get_open_orders["example"] = "{\"cmd\": \"get_open_orders\"}";
   commands["get_open_orders"].Set(get_open_orders);

   // show_symbol
   CJAVal show_symbol;
   show_symbol["description"] = "Change chart symbol";
   show_symbol["example"] = "{\"cmd\": \"show_symbol\", \"symbol\": \"XAUUSD\"}";
   commands["show_symbol"].Set(show_symbol);

   // close_positions
   CJAVal close_positions;
   close_positions["description"] = "Close all positions for a symbol";
   close_positions["example"] = "{\"cmd\": \"close_positions\", \"symbol\": \"XAUUSD\"}";
   commands["close_positions"].Set(close_positions);

   // delete_pending_buys
   CJAVal delete_pending_buys;
   delete_pending_buys["description"] = "Delete pending BUY orders (BUYLIMIT, BUYSTOP) for a symbol (defaults to current symbol)";
   delete_pending_buys["example"] = "{\"cmd\": \"delete_pending_buys\", \"symbol\": \"XAUUSD\"}";
   commands["delete_pending_buys"].Set(delete_pending_buys);

   // delete_pending_sells
   CJAVal delete_pending_sells;
   delete_pending_sells["description"] = "Delete pending SELL orders (SELLLIMIT, SELLSTOP) for a symbol (defaults to current symbol)";
   delete_pending_sells["example"] = "{\"cmd\": \"delete_pending_sells\", \"symbol\": \"XAUUSD\"}";
   commands["delete_pending_sells"].Set(delete_pending_sells);

   // BUY
   CJAVal buy;
   buy["description"] = "Open buy order";
   buy["example"] = "{\"cmd\": \"BUY\", \"symbol\": \"XAUUSD\", \"risk\": 2, \"sl_pips\": 20, \"id\": \"unique_order_id\"}";
   commands["BUY"].Set(buy);

   // SELL
   CJAVal sell;
   sell["description"] = "Open sell order";
   sell["example"] = "{\"cmd\": \"SELL\", \"symbol\": \"XAUUSD\", \"risk\": 2, \"sl_pips\": 20, \"id\": \"unique_order_id\"}";
   commands["SELL"].Set(sell);

   // new_level_added
   CJAVal new_level;
   new_level["description"] = "Open market or place pending order at level: for BUY add spread to pending; for SELL no spread. Market orders open without SL.";
   new_level["example"] = "{\"cmd\": \"new_level_added\", \"symbol\": \"XAUUSD\", \"direction\": \"BUY\", \"price\": 2350.0, \"risk\": 5, \"sl_pips\": 20, \"id\": \"level_123\"}";
   commands["new_level_added"].Set(new_level);

   // get_symbols_history
   CJAVal get_symbols_history;
   get_symbols_history["description"] = "Get historical OHLC data";
   get_symbols_history["example"] = "{\"cmd\": \"get_symbols_history\", \"symbols\": \"XAUUSD,EURUSD,GBPJPY\", \"history\": 100}";
   commands["get_symbols_history"].Set(get_symbols_history);

   // help
   CJAVal help;
   help["description"] = "Show all available commands and examples";
   help["example"] = "{\"cmd\": \"help\"}";
   commands["help"].Set(help);

   output_json["commands"].Set(commands);
   output_json["info"] = "Send any of these commands as JSON to the socket server. All responses will include 'completed' and 'cmd' fields.";
   return output_json;
}

CJAVal process_socket_command(CJAVal &input_json)
{
   CJAVal output_json;
   string cmd = input_json["cmd"].ToStr();

   if (cmd == "get_open_orders")
   {
      output_json = handle_get_open_orders(input_json);
   }
   else if (cmd == "show_symbol")
   {
      output_json = handle_show_symbol(input_json);
   }
   else if (cmd == "close_positions")
   {
      output_json = handle_close_positions(input_json);
   }
   else if (cmd == "delete_pending_buys")
   {
      output_json = handle_delete_pending_buys(input_json);
   }
   else if (cmd == "delete_pending_sells")
   {
      output_json = handle_delete_pending_sells(input_json);
   }
   else if (cmd == "BUY")
   {
      output_json = handle_buy_order(input_json);
   }
   else if (cmd == "SELL")
   {
      output_json = handle_sell_order(input_json);
   }
   else if (cmd == "new_level_added")
   {
      output_json = handle_new_level_added(input_json);
   }
   else if (cmd == "get_symbols_history")
   {
      output_json = handle_get_symbols_history(input_json);
   }
   else if (cmd == "help")
   {
      output_json = handle_help_command(input_json);
   }
   else
   {
      // Unknown command
      output_json["error"] = "Unknown command";
   }

   output_json["completed"] = "1";
   output_json["cmd"] = cmd;
   
   return output_json;
} 

// --------------------------------------------------------------------
// Use OnTick() to watch for failure to create the timer in OnInit()
// --------------------------------------------------------------------

void OnTick()
{
   if (!glbCreatedTimer) glbCreatedTimer = EventSetMillisecondTimer(TIMER_FREQUENCY_MS);
}

// --------------------------------------------------------------------
// Event-driven functionality, turned on by #defining SOCKET_LIBRARY_USE_EVENTS
// before including the socket library. This generates dummy key-down
// messages when socket activity occurs, with lparam being the 
// .GetSocketHandle()
// --------------------------------------------------------------------

void OnChartEvent(const int id, const long& lparam, const double& dparam, const string& sparam)
{
   // Handle button click
   if (id == CHARTEVENT_OBJECT_CLICK)
   {
      if (sparam == testButtonName)
      {
         Print("Test button clicked - sending test data");
         SendTestOrdersData();
         return;
      }
      else if (sparam == orderClosedTestButtonName)
      {
         Print("Order closed test button clicked - sending test order closed data");
         SendTestOrderClosedData();
         return;
      }
   }

   // Handle socket events
   if (id == CHARTEVENT_KEYDOWN) {
      // If the lparam matches a .GetSocketHandle(), then it's a dummy
      // key press indicating that there's socket activity. Otherwise,
      // it's a real key press

      if (lparam == glbServerSocket.GetSocketHandle()) {
         // Activity on server socket. Accept new connections
         Print("New server socket event - incoming connection");
         AcceptNewConnections();

      } else {
         // Compare lparam to each client socket handle
         for (int i = 0; i < ArraySize(glbClients); i++) {
            if (lparam == glbClients[i].GetSocketHandle()) {
               HandleSocketIncomingData(i);
               return; // Early exit
            }
         }

         // If we get here, then the key press does not seem
         // to match any socket, and appears to be a real
         // key press event...
      }
   }
}

CJAVal analyze_watchlist_by_symbol()
{
   CJAVal result;
   string db_filepath = data_folder + "\\" + "ripper_db.txt";
   
   // Load database directly from file
   CJAVal db_state;
   if (FileIsExist(db_filepath)) 
   {
      int file_handle = FileOpen(db_filepath, FILE_READ|FILE_TXT);
      int str_size = FileReadInteger(file_handle, INT_VALUE);
      string str = FileReadString(file_handle, str_size);
      FileClose(file_handle);
      db_state.Deserialize(str);
   }
   else
   {
      // Return empty result if database doesn't exist
      return result;
   }
   
   // Get the watchlist items
   string watch_list[];
   StringSplit(db_state["watch_list"].ToStr(), ',', watch_list);
   int count = ArraySize(watch_list);
   
   // Process each item in the watchlist
   for(int i=0; i<count; i++)
   {
      string obj_name = watch_list[i];
      
      // Skip empty object names
      if(StringLen(obj_name) == 0)
         continue;
         
      // Get the object from the database
      CJAVal obj = db_state[obj_name];
      
      // Check if it has required fields
      if(obj.HasKey("symbol") && obj.HasKey("direction"))
      {
         string symbol = obj["symbol"].ToStr();
         string direction = obj["direction"].ToStr();
         string object_type = obj.HasKey("object_type") ? obj["object_type"].ToStr() : "";
         
         // If this symbol doesn't exist in the result yet, initialize it
         if(!result.HasKey(symbol))
         {
            CJAVal symbolStats;
            symbolStats["total"] = 0;
            symbolStats["buy"] = 0;
            symbolStats["sell"] = 0;
            symbolStats["tline_total"] = 0;
            symbolStats["tline_buy"] = 0;
            symbolStats["tline_sell"] = 0;
            symbolStats["shortline_total"] = 0;
            symbolStats["shortline_buy"] = 0;
            symbolStats["shortline_sell"] = 0;
            result[symbol].Set(symbolStats);
         }
         
         // Update total counters
         result[symbol]["total"] = result[symbol]["total"].ToInt() + 1;
         
         // Update direction counters
         if(direction == "buy")
            result[symbol]["buy"] = result[symbol]["buy"].ToInt() + 1;
         else if(direction == "sell")
            result[symbol]["sell"] = result[symbol]["sell"].ToInt() + 1;
         
         // Update object type counters
         if(object_type == "tline")
         {
            result[symbol]["tline_total"] = result[symbol]["tline_total"].ToInt() + 1;
            if(direction == "buy")
               result[symbol]["tline_buy"] = result[symbol]["tline_buy"].ToInt() + 1;
            else if(direction == "sell")
               result[symbol]["tline_sell"] = result[symbol]["tline_sell"].ToInt() + 1;
         }
         else if(object_type == "shortline")
         {
            result[symbol]["shortline_total"] = result[symbol]["shortline_total"].ToInt() + 1;
            if(direction == "buy")
               result[symbol]["shortline_buy"] = result[symbol]["shortline_buy"].ToInt() + 1;
            else if(direction == "sell")
               result[symbol]["shortline_sell"] = result[symbol]["shortline_sell"].ToInt() + 1;
         }
      }
   }
   
   return result;
}

