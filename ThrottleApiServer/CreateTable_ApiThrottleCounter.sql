CREATE TABLE [dbo].[ApiThrottleCounter](
	[Id] [nvarchar](400) NOT NULL,
	[Val] [bigint] NOT NULL,
	[AddVal] [bigint] NOT NULL,
	[CreatedTime] [datetime2](7) NOT NULL,
	[ExpiresAtTime] [datetime2](7) NULL,
	[SlidingExpirationInSeconds] [bigint] NULL,
	[AbsoluteExpiration] [datetime2](7) NULL,
 CONSTRAINT [PK_ApiThrottleCounter] PRIMARY KEY CLUSTERED 
(
	[Id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]
) ON [PRIMARY]
GO
