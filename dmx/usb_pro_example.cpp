#include "pro_driver.h"
#include <Windows.h>


// old school globals
DMXUSBPROParamsType PRO_Params;
FT_HANDLE device_handle = NULL ;

#define DMX_DATA_LENGTH 513 // Includes the start code 

enum color_t {WHITE = 0, CTC = 12, YELLOW = 24, BLUE_104 = 36, PINK = 48, GREEN_206 = 60, BLUE_108 = 72, RED = 84, MAGENTA = 96, BLUE_101 = 108, GREEN_202 = 132, PURPLE = 144};


__declspec(dllexport) void setColorContinuous(unsigned char *packet, int base, color_t c){
	int COLOR_CHANNEL = 3;

	
		switch(c){
		case WHITE:
		case CTC:
		case YELLOW:
		case BLUE_104:
		case PINK:
		case GREEN_206:
		case BLUE_108:
		case RED:
		case MAGENTA:
		case BLUE_101:
		case GREEN_202:
		case PURPLE:
			packet[base + COLOR_CHANNEL - 1] = c;
			break;
		}
}

enum speed_t {FAST = 248, MEDIUM = 251, SLOW = 255};

__declspec(dllexport) void setRandomColors(unsigned char *packet, speed_t speed, int base){
	int COLOR_CHANNEL = 3;
	switch(speed){
	case FAST:
	case MEDIUM:
	case SLOW:
		packet[COLOR_CHANNEL + base - 1] = speed;
		
	}
}

__declspec(dllexport) void setLampOn(unsigned char *packet, int base){
	int DMX_LAMP_ON = 237;
	int LAMP_ON_CHANNEL = 1;

	packet[base + LAMP_ON_CHANNEL - 1] = DMX_LAMP_ON;
}

__declspec(dllexport) void setLampOff(unsigned char *packet, int base){
	int DMX_LAMP_OFF = 254;
	int DMX_LAMP_OFF_CHANNEL = 1;

	packet[base + DMX_LAMP_OFF_CHANNEL - 1] = DMX_LAMP_OFF;
}

__declspec(dllexport) void setShutterOpen(unsigned char *packet, int base){
	int DMX_SHUTTER_OPEN = 49;
	int DMX_SHUTTER_CHANNEL = 1;

	packet[base + DMX_SHUTTER_CHANNEL - 1] = DMX_SHUTTER_OPEN;
}

__declspec(dllexport) void setShutterClose(unsigned char *packet, int base){
	int DMX_SHUTTER_CLOSE = 2;
	int DMX_SHUTTER_CHANNEL = 1;

	packet[base + DMX_SHUTTER_CHANNEL - 1] = DMX_SHUTTER_CLOSE;
}

__declspec(dllexport) void setDimmerLevel(unsigned char *packet, int base, int level){
	int DMX_DIMMER_CHANNEL = 2;

	if(level >= 0 && level <= 255){
		packet[base + DMX_DIMMER_CHANNEL - 1] = level;
	}
}

//only using standard gobos for now
static int GOBO_COUNT = 8;
static int GOBO_CHANNEL = 4;
__declspec(dllexport) void setGoboStandard(unsigned char *packet, int base, int goboVal){
	int dmxGoboVals[] = {19,29,39,49,59,69,79,85};
	
	if(goboVal <= GOBO_COUNT && goboVal >= 0){
		packet[base + GOBO_CHANNEL - 1] = dmxGoboVals[goboVal - 1];
	}
}

//valid for standard and indexed mode
__declspec(dllexport) void clearGobo(unsigned char *packet, int base){
	packet[base + GOBO_CHANNEL - 1] = 0;
}
static int MAX_FOCUS = 255;
static int MIN_FOCUS = 0;
static int FOCUS_CHANNEL = 6;

//focus 0 = infinity, 255 = 2 meters
__declspec(dllexport) void setFocus(unsigned char *packet, int base, int focus){
	if(focus >= MIN_FOCUS && focus <= MAX_FOCUS){
		packet[base + FOCUS_CHANNEL - 1] = focus;
	}
}

static int PRISM_CHANNEL = 7;
__declspec(dllexport) void setPrismOff(unsigned char *packet, int base){
	packet[base + PRISM_CHANNEL - 1] = 0;
}

//intensity range 0-59
enum rotation_direction_t {CW, CCW};

__declspec(dllexport) void setPrismRotate(unsigned char *packet, int base, rotation_direction_t direction, int intensity){
	int temp;
	if(direction == CCW){
			packet[PRISM_CHANNEL + base - 1] = intensity + 20;
	}else if(direction == CW){
			packet[PRISM_CHANNEL + base - 1] = intensity + 90;
	}
	//else not a valid direction
}

//no prism/gobo macros for now

static int NEUTRAL = 128;
static int MIN_PAN = -128;
static int MAX_PAN = 127;
static int PAN_CHANNEL = 8;

//negative values go left, positive right, 0 is neutral
__declspec(dllexport) void setPan(unsigned char *packet, int base, int panVal){
	if(panVal >= MIN_PAN && panVal <= MAX_PAN){
		packet[PAN_CHANNEL + base - 1] = panVal + 128;
	}
}

static int MIN_TILT = -128;
static int MAX_TILT = 127;
static int TILT_CHANNEL = 10;

__declspec(dllexport) void setTilt(unsigned char *packet, int base, int tiltVal){
	if(tiltVal >= MIN_TILT && tiltVal <= MAX_TILT){
		packet[TILT_CHANNEL + base - 1] = tiltVal + 128;
	}
}






/* Function : FTDI_ClosePort
 * Author	: ENTTEC
 * Purpose  : Closes the Open DMX USB PRO Device Handle
 * Parameters: none
 **/
void FTDI_ClosePort()
{
	if (device_handle != NULL)
		FT_Close(device_handle);
}

/* Function : FTDI_ListDevices
 * Author	: ENTTEC
 * Purpose  : Returns the no. of PRO's conneced to the PC
 * Parameters: none
 **/
int FTDI_ListDevices()
{
	FT_STATUS ftStatus;
	DWORD numDevs=0;
	ftStatus = FT_ListDevices((PVOID)&numDevs,NULL,FT_LIST_NUMBER_ONLY);
	if(ftStatus == FT_OK) 
		return numDevs;
	return NO_RESPONSE; 
}


/* Function : FTDI_SendData
 * Author	: ENTTEC
 * Purpose  : Send Data (DMX or other packets) to the PRO
 * Parameters: Label, Pointer to Data Structure, Length of Data
 **/
int FTDI_SendData(int label, unsigned char *data, int length)
{
	unsigned char end_code = DMX_END_CODE;
	FT_STATUS res = 0;
	DWORD bytes_to_write = length;
	DWORD bytes_written = 0;
	HANDLE event = NULL;
	int size=0;
	// Form Packet Header
	unsigned char header[DMX_HEADER_LENGTH];
	header[0] = DMX_START_CODE;
	header[1] = label;
	header[2] = length & OFFSET;
	header[3] = length >> BYTE_LENGTH;
	// Write The Header
	res = FT_Write(	device_handle,(unsigned char *)header,DMX_HEADER_LENGTH,&bytes_written);
	if (bytes_written != DMX_HEADER_LENGTH) return  NO_RESPONSE;
	// Write The Data
	res = FT_Write(	device_handle,(unsigned char *)data,length,&bytes_written);
	if (bytes_written != length) return  NO_RESPONSE;
	// Write End Code
	res = FT_Write(	device_handle,(unsigned char *)&end_code,ONE_BYTE,&bytes_written);
	if (bytes_written != ONE_BYTE) return  NO_RESPONSE;
	if (res == FT_OK)
		return TRUE;
	else
		return FALSE; 
}

/* Function : FTDI_ReceiveData
 * Author	: ENTTEC
 * Purpose  : Receive Data (DMX or other packets) from the PRO
 * Parameters: Label, Pointer to Data Structure, Length of Data
 **/
int FTDI_ReceiveData(int label, unsigned char *data, unsigned int expected_length)
{

	FT_STATUS res = 0;
	DWORD length = 0;
	DWORD bytes_to_read = 1;
	DWORD bytes_read =0;
	unsigned char byte = 0;
	HANDLE event = NULL;
	char buffer[600];
	// Check for Start Code and matching Label
	while (byte != label)
	{
		while (byte != DMX_START_CODE)
		{
			res = FT_Read(device_handle,(unsigned char *)&byte,ONE_BYTE,&bytes_read);
			if(bytes_read== NO_RESPONSE) return  NO_RESPONSE;
		}
		res = FT_Read(device_handle,(unsigned char *)&byte,ONE_BYTE,&bytes_read);
		if (bytes_read== NO_RESPONSE) return  NO_RESPONSE;
	}
	// Read the rest of the Header Byte by Byte -- Get Length
	res = FT_Read(device_handle,(unsigned char *)&byte,ONE_BYTE,&bytes_read);
	if (bytes_read== NO_RESPONSE) return  NO_RESPONSE;
	length = byte;
	res = FT_Read(device_handle,(unsigned char *)&byte,ONE_BYTE,&bytes_read);
	if (res != FT_OK) return  NO_RESPONSE;
	length += ((uint32_t)byte)<<BYTE_LENGTH;	
	// Check Length is not greater than allowed
	if (length > DMX_PACKET_SIZE)
		return  NO_RESPONSE;
	// Read the actual Response Data
	res = FT_Read(device_handle,buffer,length,&bytes_read);
	if(bytes_read != length) return  NO_RESPONSE;
	// Check The End Code
	res = FT_Read(device_handle,(unsigned char *)&byte,ONE_BYTE,&bytes_read);
	if(bytes_read== NO_RESPONSE) return  NO_RESPONSE;
	if (byte != DMX_END_CODE) return  NO_RESPONSE;
	// Copy The Data read to the buffer passed
	memcpy(data,buffer,expected_length);
	return TRUE;
}

/* Function : FTDI_PurgeBuffer
 * Author	: ENTTEC
 * Purpose  : Clears the buffer used internally by the PRO
 * Parameters: none
 **/
void FTDI_PurgeBuffer()
{
	FT_Purge (device_handle,FT_PURGE_TX);
	FT_Purge (device_handle,FT_PURGE_RX);
}


/* Function : FTDI_OpenDevice
 * Author	: ENTTEC
 * Purpose  : Opens the PRO; Tests various parameters; outputs info
 * Parameters: device num (returned by the List Device fuc), Fw Version MSB, Fw Version LSB 
 **/
uint16_t FTDI_OpenDevice(int device_num)
{
	int RTimeout =120;
	int WTimeout =100;
	int VersionMSB =0;
	int VersionLSB =0;
	uint8_t temp[4];
	long version;
	uint8_t major_ver,minor_ver,build_ver;
	int recvd =0;
	unsigned char byte = 0;
	int size = 0;
	int res = 0;
	int tries =0;
	uint8_t latencyTimer;
	FT_STATUS ftStatus;
	int BreakTime;
	int MABTime;
	// Try at least 3 times 
	do  {
		printf("\n------ D2XX ------- Opening [Device %d] ------ Try %d",device_num,tries);
		// Open the PRO 
		ftStatus = FT_Open(device_num,&device_handle);
		// delay for next try
		Sleep(750);
		tries ++;
	} while ((ftStatus != FT_OK) && (tries < 3)); 
	// PRO Opened succesfully
	if (ftStatus == FT_OK) 
	{
		// GET D2XX Driver Version
		ftStatus = FT_GetDriverVersion(device_handle,(LPDWORD)&version);
		if (ftStatus == FT_OK) 
		{
			major_ver = (uint8_t) version >> 16;
			minor_ver = (uint8_t) version >> 8;
			build_ver = (uint8_t) version & 0xFF;
			printf("\nD2XX Driver Version:: %02X.%02X.%02X ",major_ver,minor_ver,build_ver);
		}
		else
			printf("Unable to Get D2XX Driver Version") ;

		// GET Latency Timer
		ftStatus = FT_GetLatencyTimer (device_handle,(PUCHAR)&latencyTimer);
		if (ftStatus == FT_OK) 
			printf("\nLatency Timer:: %d ",latencyTimer);		
		else
			printf("\nUnable to Get Latency Timer") ;
		// SET Default Read & Write Timeouts (in micro sec ~ 100)
		FT_SetTimeouts(device_handle,RTimeout,WTimeout);
		// Piurges the buffer
		FT_Purge (device_handle,FT_PURGE_RX);
		// Send Get Widget Parameters to get Device Info
		printf("Sending GET_WIDGET_PARAMS packet... ");
 		res = FTDI_SendData(GET_WIDGET_PARAMS,(unsigned char *)&size,2);
		// Check Response
		if (res == NO_RESPONSE)
		{
			FT_Purge (device_handle,FT_PURGE_TX);
 			res = FTDI_SendData(GET_WIDGET_PARAMS,(unsigned char *)&size,2);
			if (res == NO_RESPONSE)
			{
				FTDI_ClosePort();
				return  NO_RESPONSE;
			}
		}
		else
			printf("\n PRO Connected Succesfully");
		// Receive Widget Response
		printf("\nWaiting for GET_WIDGET_PARAMS_REPLY packet... ");
		res=FTDI_ReceiveData(GET_WIDGET_PARAMS_REPLY,(unsigned char *)&PRO_Params,sizeof(DMXUSBPROParamsType));
		// Check Response
		if (res == NO_RESPONSE)
		{
			// Recive Widget Response packet
			res=FTDI_ReceiveData(GET_WIDGET_PARAMS_REPLY,(unsigned char *)&PRO_Params,sizeof(DMXUSBPROParamsType));
			if (res == NO_RESPONSE)
			{
				FTDI_ClosePort();
				return  NO_RESPONSE;
			}
		}
		else
			printf("\n GET WIDGET REPLY Received ... ");
		// Firmware  Version
		VersionMSB = PRO_Params.FirmwareMSB;
		VersionLSB = PRO_Params.FirmwareLSB;
		// GET PRO's serial number 
		res = FTDI_SendData(GET_WIDGET_SN,(unsigned char *)&size,2);
		res=FTDI_ReceiveData(GET_WIDGET_SN,(unsigned char *)&temp,4);
		// Display All PRO Parametrs & Info avialable
		printf("\n-----------::PRO Connected [Information Follows]::------------");
		printf("\n\t\t  FIRMWARE VERSION: %d.%d",VersionMSB,VersionLSB);
		BreakTime = (int) (PRO_Params.BreakTime * 10.67) + 100;
		printf("\n\t\t  BREAK TIME: %d micro sec ",BreakTime);
		MABTime = (int) (PRO_Params.MaBTime * 10.67);
		printf("\n\t\t  MAB TIME: %d micro sec",MABTime);
		printf("\n\t\t  SEND REFRESH RATE: %d packets/sec",PRO_Params.RefreshRate);
		// return success
		return TRUE;
	}		
	else // Can't open Device 
		return FALSE;
}


// Read a DMX packet
uint8_t FTDI_RxDMX(uint8_t label, unsigned char *data, uint32_t* expected_length)
{

	FT_STATUS res = 0;
	DWORD length = 0;
	DWORD bytes_to_read = 1;
	DWORD bytes_read =0;
	unsigned char byte = 0;
	unsigned char header[3];
	HANDLE event = NULL;
	unsigned char buffer[600];
	// Check for Start Code and matching Label
	while (byte != DMX_START_CODE)
	{
		res = FT_Read(device_handle,(unsigned char *)&byte,ONE_BYTE,&bytes_read);
		if(bytes_read== NO_RESPONSE) return  NO_RESPONSE;
	}
	res = FT_Read(device_handle,header,3,&bytes_read);
	if(bytes_read== NO_RESPONSE) return  NO_RESPONSE;
	if(header[0] != label) return NO_RESPONSE;
	length = header[1];
	length += ((uint32_t)header[2])<<BYTE_LENGTH;	
	length += 1;
	// Check Length is not greater than allowed
	if (length > DMX_PACKET_SIZE +3)
		return  NO_RESPONSE;
	// Read the actual Response Data
	res = FT_Read(device_handle,buffer,length,&bytes_read);
	if(bytes_read != length) return  NO_RESPONSE;
	// Check The End Code
	if (buffer[length-1] != DMX_END_CODE) return  NO_RESPONSE;
	*expected_length = (unsigned int)length;
	// Copy The Data read to the buffer passed
	memcpy(data,buffer,*expected_length);
	return TRUE;
}

// our good main function with everything to do the test
int main(int argc, char**argv)
{
	// declare all needed variables 
	WORD wVID = 0x0403;
	WORD wPID = 0x6001;
	uint8_t Num_Devices =0;
	uint16_t device_connected =0;
	uint32_t length =DMX_DATA_LENGTH;
	int i=0;
	int device_num=0;
	BOOL res = 0;
	// startup message for example
	printf("\nEnttec Pro - C - Windows - FTDI Test\n");
	printf("\nLooking for a PRO's connected to PC ... ");
	Num_Devices = FTDI_ListDevices(); 
	// Number of Found Devices
	if (Num_Devices == 0)
	{
		printf("\n Looking for Devices  - 0 Found");
	}
	else
	{
		printf("\n Looking for Devices  - %d Found",Num_Devices);
		// Just to make sure the Device is correct
		printf("\n Intialize 1st Device :");
		//getch();
		 // we'll open the first one only
			device_num = 0;
			device_connected = FTDI_OpenDevice(device_num);

		// If you want to open all; use for loop ; uncomment the folllowing 
		/*
		 for (i=0;i<Num_Devices;i++)
		 {
			if (device_connected)
				break;
			device_num = i;
			device_connected = FTDI_OpenDevice(device_num);		
		 }
		 */

		// Send DMX Code
		if (device_connected) 
		{
			unsigned char myDmx[DMX_DATA_LENGTH];
			printf("\n Press Enter to Send DMX data :");
			getch();
			
			for(int i = 0; i < DMX_DATA_LENGTH; i++){
				myDmx[i] = 0;
			}
			setLampOn(myDmx, 150);

			FTDI_SendData(SET_DMX_TX_MODE, myDmx, DMX_DATA_LENGTH);
			Sleep(500);
			//setShutterOpen(myDmx, 150);
			setDimmerLevel(myDmx, 150, 255);
			setFocus(myDmx, 150, 108); //85 chem, 80 field
			
			setRandomColors(myDmx, FAST, 150);
			//setPrismRotate(myDmx, 150, CCW, 30);
			Sleep(500);
			FTDI_SendData(SET_DMX_TX_MODE, myDmx, DMX_DATA_LENGTH);
			int gobos = 4;
			int focus = 0;
			while(true){
				myDmx[0] = 0;
				
				FTDI_SendData(SET_DMX_TX_MODE, myDmx, DMX_DATA_LENGTH);
				setGoboStandard(myDmx, 150, gobos);
				gobos = (gobos + 1) % 8;

				for(int i = 0; i < 128; i++){
					Sleep(100);
					setTilt(myDmx, 150, i);
					setPan(myDmx, 150, i - 50);
					for(int k = 150; k <163; k++){
						printf("i:%d  val: %d\n", k, myDmx[k]);
					}
					//printf("give me a focus: ");
					//scanf("%d", &focus);
					//setFocus(myDmx, 150, focus);
					//setGoboStandard(myDmx, 150, 2);
					FTDI_SendData(SET_DMX_TX_MODE, myDmx, DMX_DATA_LENGTH);
			
				}
			}
			getch();


			// Looping to Send DMX data
			//for (int i = 0; i < 1000 ; i++)
			//{
			//	// initialize with data to send
			//	memset(myDmx,i,DMX_DATA_LENGTH);
			//	// Start Code = 0
			//	myDmx[0] = 0;
			//	// actual send function called 
			//	res = FTDI_SendData(SET_DMX_TX_MODE, myDmx, DMX_DATA_LENGTH);
			//	// check response from Send function
			//	if (res < 0)
			//	{
			//		printf("FAILED: Sending DMX to PRO \n");
			//		FTDI_ClosePort();
			//		break;
			//		return -1;
			//	}
			//	// output debug
			//	printf("Iteration: %d\n", i);
			//	printf("DMX Data SENT from 0 to 10: ");
			//	for (int j = 0; j <= 8; j++)
			//		printf (" %d ",myDmx[j]);				
			//}
		}

		// DMX Receive Code in a loop 
		if (device_connected)
		{
			unsigned char myDmxIn[DMX_DATA_LENGTH];
			// Looping to receiving DMX data
			printf("\n Press Enter to receive DMX data :");
			getch();
			for (int i = 0; i < 1000 ; i++)
			{
				// re-initailize before each read
				memset(myDmxIn,0,DMX_DATA_LENGTH);
				// actual recieve function called
				res = FTDI_RxDMX(SET_DMX_RX_MODE, myDmxIn, &length);
				// check response from Receive function
				if (res < 0)
				{
					printf("FAILED: Reading DMX from PRO \n");
					FTDI_ClosePort();
					break;
					return -1;
				}
				// output debug
				printf("Length Recieved: %d\n", length);
				printf("DMX Data from 0 to 10: ");
				for (int j = 0; j <= 20; j++)
					printf (" %d",myDmxIn[j]);
			}
		}
	}

	// Finish all done
	printf("\n Press Enter to Exit :");
	getch();

	return 0;
}