param ([Parameter(Mandatory)]$isstable, [Parameter(Mandatory)]$release)

if ($isstable -eq "stable") {
	write-host "Releasing stable" 
	
	vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel win-x64-stable
	vpk pack -u VesperApp -v $release --packTitle 'Vesper App' --icon Assets\bat.ico -p bin\publish\Win-x64-stable --channel win-x64-stable
	vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel win-x64-stable

	#vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel linux-x64-stable
	#vpk pack -u VesperApp -v $release -p bin\publish\Linux-x64-stable --channel linux-x64-stable
	#vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel linux-x64-stable

	#vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel osx-x64-stable
	#vpk pack -u VesperApp -v $release -p bin\publish\OsX-x64-stable --channel osx-x64-stable
	#vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel osx-x64-stable

} else {
	write-host "Releasing beta"

	vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel win-x64-beta
	vpk pack -u VesperApp -v $release  --packTitle 'Vesper App' --icon Assets\bat.ico -p bin\publish\Win-x64-beta --channel win-x64-beta
	vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel win-x64-beta

	#vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel linux-x64-beta
	#vpk pack -u VesperApp -v $release -p bin\publish\Linux-x64-beta  --channel linux-x64-beta
	#vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel linux-x64-beta

	#vpk download s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel osx-x64-beta
	#vpk pack -u VesperApp -v $release -p bin\publish\OsX-x64-beta --channel osx-x64-beta
	#vpk upload s3 --bucket vesperapprelease --region eu-central-1 --keyId AKIAVMDPSNQJR2OBZRVG --secret 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 --channel osx-x64-beta
}
