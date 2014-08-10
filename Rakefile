begin
  require 'albacore'
rescue LoadError
  warn "Please run 'gem install albacore --pre'"
  exit 1
end

CONFIG = ENV['CONFIG'] || 'Debug'

COVERAGE_DIR = 'Coverage'
COVERAGE_FILE = File.join(COVERAGE_DIR, 'coverage.xml')

directory COVERAGE_DIR

desc "Build BitPacker.sln using the current CONFIG (or Debug)"
build :build do |b|
  b.sln = "BitPacker.sln"
  b.target = [:Build]
  b.prop 'Configuration', CONFIG
end

task :test_environment => [:build] do
  NUNIT_TOOLS = 'packages/NUnit.Runners.*/tools'
  NUNIT_CONSOLE = Dir[File.join(NUNIT_TOOLS, 'nunit-console.exe')].first
  NUNIT_EXE = Dir[File.join(NUNIT_TOOLS, 'nunit.exe')].first

  OPENCOVER_CONSOLE = Dir['packages/OpenCover.*/OpenCover.Console.exe'].first
  REPORT_GENERATOR = Dir['packages/ReportGenerator.*/ReportGenerator.exe'].first

  UNIT_TESTS_DLL = "BitPackerUnitTests/bin/#{CONFIG}/BitPackerUnitTests.dll"

  raise "NUnit.Runners not found. Restore NuGet packages" unless NUNIT_CONSOLE && NUNIT_EXE
  raise "OpenCover not found. Restore NuGet packages" unless OPENCOVER_CONSOLE
  raise "ReportGenerator not found. Restore NuGet packages" unless REPORT_GENERATOR
end

test_runner :nunit_test_runner => [:test_environment] do |t|
  t.exe = NUNIT_CONSOLE
  t.files = [UNIT_TESTS_DLL]
  t.add_parameter '/nologo'
end

desc "Run unit tests using the current CONFIG (or Debug)"
task :test => [:nunit_test_runner] do |t|
  rm 'TestResult.xml', :force => true
end

desc "Launch the NUnit gui pointing at the correct DLL for CONFIG (or Debug)"
task :nunit => [:test_environment] do |t|
  sh NUNIT_EXE, UNIT_TESTS_DLL
end


desc "Generate unit test code coverage reports for CONFIG (or Debug)"
task :cover => [:test_environment, COVERAGE_DIR] do |t|
	coverage(instrument(:nunit, UNIT_TESTS_DLL))
end


def instrument(runner, target)
  case runner
  when :nunit
    opttarget = NUNIT_CONSOLE
    opttargetargs = target
  when :exe
    opttarget = target
    opttargetargs = ''
  else
    raise "Unknown runner #{runner}"
  end
 
  coverage_file = File.join(COVERAGE_DIR, File.basename(target).ext('xml'))
  sh OPENCOVER_CONSOLE, %Q{-register:user -target:"#{opttarget}" -filter:"+[BitPacker]* -targetargs:"#{opttargetargs} /noshadow" -output:"#{coverage_file}"}

  rm('TestResult.xml', :force => true) if runner == :nunit

  coverage_file
end

def coverage(coverage_files)
  coverage_files = [*coverage_files]
  sh REPORT_GENERATOR, %Q{-reports:"#{coverage_files.join(';')}" "-targetdir:#{COVERAGE_DIR}"}
end


