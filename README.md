# Tgstation.Server.ControlPanel

[![Build status](https://ci.appveyor.com/api/projects/status/gl54ng7129t4o5ca/branch/master?svg=true)](https://ci.appveyor.com/project/Cyberboss/Tgstation.Server.ControlPanel/branch/master) [![Waffle.io - Columns and their card count](https://badge.waffle.io/tgstation/Tgstation.Server.ControlPanel.svg?columns=all)](https://waffle.io/tgstation/Tgstation.Server.ControlPanel)

[![GitHub license](https://img.shields.io/github/license/tgstation/Tgstation.Server.ControlPanel.svg)](https://github.com/tgstation/Tgstation.Server.ControlPanel/blob/master/LICENSE) [![Average time to resolve an issue](http://isitmaintained.com/badge/resolution/tgstation/Tgstation.Server.ControlPanel.svg)](http://isitmaintained.com/project/tgstation/Tgstation.Server.ControlPanel "Average time to resolve an issue")

[![forthebadge](http://forthebadge.com/images/badges/made-with-c-sharp.svg)](http://forthebadge.com) [![forthebadge](http://forthebadge.com/images/badges/60-percent-of-the-time-works-every-time.svg)](http://forthebadge.com)

Official management suite for tgstation-server

## Installing

### Windows

First, ensure you have the ASP .NET Core 6.0.X runtime installed. See [here](https://dotnet.microsoft.com/download/dotnet/6.0) for download.


Once that's done, [Click Here](https://github.com/tgstation/Tgstation.Server.ControlPanel/releases/latest) for a download link to the self updating version.

### OSX/Linux

To install the latest version of the code

1. Download and install the [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
1. Clone this repository
1. Navigate to `src/Tgstation.Server.ControlPanel`
1. Run `dotnet publish -c Release -o <Your chosen installation directory>`

To run the installation

1. Navigate to your chosen installation directory
2. Run `dotnet Tgstation.Server.ControlPanel.dll`

## Usage

Please help this project out by contributing to this documentation

## OAuth

To enable OAuth logins with the control panel, set your TGS OAuth configuration `RedirectUrl` property to `http://localhost:<port>` where `<port>` is any free port.
