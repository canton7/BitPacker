CONFIG = ENV['CONFIG'] || 'Debug'

COVERAGE_DIR = 'Coverage'
COVERAGE_FILE = File.join(COVERAGE_DIR, 'coverage.xml')

ASSEMBLY_INFO = 'BitPacker/Properties/AssemblyInfo.cs'

MSBUILD = %q{C:\Program Files (x86)\MSBuild\12.0\Bin\MSBuild.exe}
SLN = 'BitPacker.sln'

directory COVERAGE_DIR

desc "Bump version number"
task :version, [:version] do |t, args|
  parts = args[:version].split('.')
  parts << '0' if parts.length == 3
  version = parts.join('.')

  content = IO.read(ASSEMBLY_INFO)
  content[/^\[assembly: AssemblyVersion\(\"(.+?)\"\)\]/, 1] = version
  content[/^\[assembly: AssemblyFileVersion\(\"(.+?)\"\)\]/, 1] = version
  File.open(ASSEMBLY_INFO, 'w'){ |f| f.write(content) }

  # content = IO.read(NUSPEC)
  # content[/<version>(.+?)<\/version>/, 1] = args[:version]
  # File.open(NUSPEC, 'w'){ |f| f.write(content) }
end

desc "Build the project for release"
task :build do
  sh MSBUILD, SLN, "/t:Clean;Rebuild", "/p:Configuration=Release", "/verbosity:normal"
end

task :test_environment do
  XUNIT_CONSOLE = Dir['packages/xunit.runner.console.*/tools/xunit.console.exe'].first

  OPENCOVER_CONSOLE = Dir['packages/OpenCover.*/tools/OpenCover.Console.exe'].first
  REPORT_GENERATOR = Dir['packages/ReportGenerator.*/tools/ReportGenerator.exe'].first

  UNIT_TESTS_DLL = "BitPackerUnitTests/bin/#{CONFIG}/BitPackerUnitTests.dll"

  raise "xunit.runners.console not found. Restore NuGet packages" unless XUNIT_CONSOLE
  raise "OpenCover not found. Restore NuGet packages" unless OPENCOVER_CONSOLE
  raise "ReportGenerator not found. Restore NuGet packages" unless REPORT_GENERATOR
end

desc "Generate unit test code coverage reports for CONFIG (or Debug)"
task :cover => [:test_environment, COVERAGE_DIR] do
  coverage_file = File.join(COVERAGE_DIR, File.basename(UNIT_TESTS_DLL).ext('xml'))
  sh %Q{#{OPENCOVER_CONSOLE} -register:user -target:"#{XUNIT_CONSOLE}" -targetargs:"#{UNIT_TESTS_DLL} -noshadow" -filter:"+[BitPacker]*" -output:"#{coverage_file}"}

  rm('TestResult.xml', :force => true)

  sh %Q{#{REPORT_GENERATOR} -reports:"#{coverage_file}" -targetdir:#{COVERAGE_DIR}}
end
