/**
 *  Packet between Devices and BioStar Server
 *
 *  @author jylee@suprema.co.kr
 *  @see
 */

/*
 *  Copyright (c) 2014 Suprema Co., Ltd. All Rights Reserved.
 *
 *  This software is the confidential and proprietary information of
 *  Suprema Co., Ltd. ("Confidential Information").  You shall not
 *  disclose such Confidential Information and shall use it only in
 *  accordance with the terms of the license agreement you entered into
 *  with Suprema.
 */

#ifndef __BS2_UDP_DISCOVER_H__
#define __BS2_UDP_DISCOVER_H__

#include "BS2Types.h"
#include "config/BS2IpConfig.h"
#include "config/BS2Rs485Config.h"

/**
 *	Option structure for discovering slave devices on RS485 network
 */
typedef struct {

} BS2UdpDiscoverOption;

/**
 *	Response structure for discovering slave devices on RS485 network
 *
 *	@note	payload will be array of this structure
 */
typedef struct {
	BS2_DEVICE_ID		deviceID;			///< 4 bytes
	BS2_DEVICE_TYPE		deviceType;			///< 2 bytes
	BS2_BOOL			useDHCP;			///< 1 byte
	BS2_CONNECTION_MODE	connectionMode;		///< 1 byte
	BS2_IPV4_ADDR		deviceAddr;			///< 16 bytes
	BS2_PORT			devicePort;			///< 2 bytes
	BS2_RS485_MODE		rs485Mode;			///< 1 byte
	uint8_t			sslSupported:1;		///< 1 bit
	uint8_t			rootCertExist:1;			///< 1 bit
	uint8_t			dualIDSupported:1;		///< 1 bit
	uint8_t			useAlphanumericID:1;		///< 1 bit
	uint8_t			reserved:4;		///< 4 bits (packing)
	uint32_t				connectedIP;				///< 4 bytes : 0xFFFFFFFF if disconnected.
} BS2UdpDiscoverResponse;


typedef struct {
	BS2_DEVICE_ID	    deviceID;                               ///< 4 bytes
	BS2_DEVICE_TYPE	    deviceType;                             ///< 2 bytes
	BS2_BOOL            useDHCP;                                ///< 1 byte
	BS2_CONNECTION_MODE connectionMode;                         ///< 1 byte
	BS2_IPV4_ADDR       deviceAddr;                             ///< 16 bytes
	BS2_PORT            devicePort;                             ///< 2 bytes
	BS2_RS485_MODE      rs485Mode[BS2_RS485_MAX_CHANNELS_EX];   ///< 8 byte
	uint8_t             sslSupported:1;							///< 1 bit
	uint8_t             rootCertExist:1;                        ///< 1 bit
	uint8_t             dualIDSupported:1;                      ///< 1 bit
	uint8_t             useAlphanumericID:1;                    ///< 1 bit
	uint8_t             reservedbit:4;                          ///< 4 bits (packing)
	uint32_t            connectedIP;                            ///< 4 bytes : 0xFFFFFFFF if disconnected.
	uint8_t             reserved;
} BS2UdpDiscoverResponseEX;

#endif	// __BS2_UDP_DISCOVER_H__
