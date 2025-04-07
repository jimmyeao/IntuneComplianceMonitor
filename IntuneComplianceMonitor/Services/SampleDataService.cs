using IntuneComplianceMonitor.ViewModels;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace IntuneComplianceMonitor.Services
{
    // This is a temporary service to provide sample data until 
    // Microsoft Graph integration is fully working
    public class SampleDataService
    {
        public Task<List<DeviceViewModel>> GetDevicesAsync()
        {
            var devices = new List<DeviceViewModel>
            {
                new DeviceViewModel
                {
                    DeviceId = "DEV001",
                    DeviceName = "DESKTOP-A1",
                    Owner = "John Smith",
                    DeviceType = "Desktop",
                    LastCheckIn = DateTime.Now.AddDays(-2),
                    Ownership = "Corporate",
                    OSVersion = "Windows 11 22H2",
                    SerialNumber = "SN12345678",
                    Manufacturer = "Dell",
                    Model = "OptiPlex 7090",
                    ComplianceIssues = new List<string> { "Outdated OS", "Missing security patches" }
                },
                new DeviceViewModel
                {
                    DeviceId = "DEV002",
                    DeviceName = "LAPTOP-B2",
                    Owner = "Jane Doe",
                    DeviceType = "Laptop",
                    LastCheckIn = DateTime.Now.AddDays(-1),
                    Ownership = "Corporate",
                    OSVersion = "Windows 11 23H2",
                    SerialNumber = "SN87654321",
                    Manufacturer = "HP",
                    Model = "EliteBook 840",
                    ComplianceIssues = new List<string>()
                },
                new DeviceViewModel
                {
                    DeviceId = "DEV003",
                    DeviceName = "MOBILE-C3",
                    Owner = "Alex Johnson",
                    DeviceType = "Mobile",
                    LastCheckIn = DateTime.Now.AddDays(-10),
                    Ownership = "BYOD",
                    OSVersion = "Android 14",
                    SerialNumber = "SN55556666",
                    Manufacturer = "Samsung",
                    Model = "Galaxy S22",
                    ComplianceIssues = new List<string> { "Jailbroken device", "Unregistered device" }
                },
                new DeviceViewModel
                {
                    DeviceId = "DEV004",
                    DeviceName = "TABLET-D4",
                    Owner = "Sarah Williams",
                    DeviceType = "Tablet",
                    LastCheckIn = DateTime.Now.AddDays(-5),
                    Ownership = "BYOD",
                    OSVersion = "iOS 17.4",
                    SerialNumber = "SN99998888",
                    Manufacturer = "Apple",
                    Model = "iPad Pro",
                    ComplianceIssues = new List<string> { "Out-of-date antivirus" }
                },
                new DeviceViewModel
                {
                    DeviceId = "DEV005",
                    DeviceName = "DESKTOP-E5",
                    Owner = "Mike Brown",
                    DeviceType = "Desktop",
                    LastCheckIn = DateTime.Now.AddDays(-15),
                    Ownership = "Corporate",
                    OSVersion = "Windows 10 21H2",
                    SerialNumber = "SN11112222",
                    Manufacturer = "Lenovo",
                    Model = "ThinkCentre M70q",
                    ComplianceIssues = new List<string> { "Outdated OS", "Unauthorized software" }
                }
            };

            return Task.FromResult(devices);
        }
    }
}